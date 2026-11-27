using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The OpenID Connect authorization code flow with PKCE, as a confidential client: the one home of the flow that
    /// <see cref="IOpenIdConnectAuthService"/> and provider packages such as <c>EasyReasy.Auth.Microsoft</c> are built on.
    /// An application uses <see cref="IOpenIdConnectAuthService"/>; this type is for a package that adds a provider's
    /// own rules.
    /// </summary>
    /// <remarks>
    /// Stateless: Begin seals everything completion needs (the PKCE verifier, the nonce, the provider, the rules) in an
    /// encrypted <c>state</c> and binds it to the browser with a cookie that completion clears, and the provider refuses
    /// a code redeemed before. Build one per state key and keep it for the application's lifetime: it caches each
    /// provider's discovery document and keys.
    /// </remarks>
    public sealed class OpenIdConnectCodeFlow
    {
        /// <summary>
        /// The name of the <see cref="HttpClient"/> the flow calls providers with (discovery documents, key sets and token
        /// endpoints). Configure it with <c>services.AddHttpClient(name)</c> to add a proxy or a timeout.
        /// </summary>
        public const string HttpClientName = "EasyReasy.Auth.OpenIdConnect";

        /// <summary>
        /// The name prefix of the binding cookie every flow of this engine sets, a provider package's included, so one
        /// browser holds at most <see cref="FlowBindingCookie.MaxPendingFlows"/> of them across every OpenID Connect
        /// provider.
        /// </summary>
        public const string BindingCookieNamePrefix = "__Host-easyreasy-oidc-";

        /// <summary>
        /// The binding cookie every flow of this engine sets.
        /// </summary>
        private static readonly FlowBindingCookie BindingCookie = new FlowBindingCookie(BindingCookieNamePrefix);

        /// <summary>
        /// The claims the request's max age, acr and amr rules ask for, which a required claim may not name.
        /// </summary>
        private static readonly IReadOnlySet<string> ReservedClaimRequests = new HashSet<string>(StringComparer.Ordinal) { "auth_time", "acr", "amr" };

        /// <summary>
        /// The required claim values of a request or state that names none.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> NoRequiredClaims = new Dictionary<string, string>();

        private readonly TimeSpan _stateLifetime;
        private readonly JsonSealer _stateSealer;
        private readonly OpenIdConnectDiscovery _discovery;
        private readonly OpenIdConnectTokenEndpointClient _tokenEndpointClient;
        private readonly OpenIdConnectIdTokenValidator _idTokenValidator;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Builds a flow. The state key is copied now.
        /// </summary>
        /// <param name="options">The state key and lifetime.</param>
        /// <param name="httpClientFactory">Creates the <see cref="HttpClientName"/> client.</param>
        /// <param name="timeProvider">The clock state expiry, token lifetime and the max authentication age are checked
        /// against.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">The options are invalid (<see cref="OpenIdConnectAuthOptions.FindProblems"/>),
        /// every problem reported at once.</exception>
        public OpenIdConnectCodeFlow(OpenIdConnectAuthOptions options, IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(timeProvider);

            ArgumentProblems.ThrowIfAny(options.FindProblems(), nameof(OpenIdConnectAuthOptions), nameof(options));

            _stateLifetime = options.StateLifetime;
            // Its own sealer under the state key, never a cipher from DI, which may be the one protecting MFA secrets.
            _stateSealer = new JsonSealer(options.StateKey);
            _discovery = new OpenIdConnectDiscovery(httpClientFactory);
            _tokenEndpointClient = new OpenIdConnectTokenEndpointClient(httpClientFactory);
            _idTokenValidator = new OpenIdConnectIdTokenValidator(_discovery, timeProvider);
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Begins a flow: resolves the provider the request names, builds its authorize URL and the encrypted
        /// <c>state</c>, and binds the flow to the browser by setting a short-lived cookie on
        /// <paramref name="httpContext"/>'s response.
        /// </summary>
        /// <param name="request">What the flow asks of the provider and what its completion will check.</param>
        /// <param name="resolver">Resolves <see cref="OpenIdConnectAuthBeginRequest.ProviderKey"/>; completion resolves the
        /// same key again.</param>
        /// <param name="httpContext">The request from the browser that will complete the flow.</param>
        /// <param name="cancellationToken">Cancels resolving the provider and fetching its discovery document.</param>
        /// <returns>The authorize URL to send the browser to, and the <c>state</c>; or null when the provider key is blank
        /// or resolves to no provider, which may be the browser's doing (no cookie is set).</returns>
        /// <exception cref="ArgumentException">The request is invalid for the provider, every problem reported at once. No
        /// cookie is set.</exception>
        /// <exception cref="InvalidOperationException">The provider's discovery document could not be retrieved or is not
        /// one it may use.</exception>
        public async Task<OpenIdConnectAuthBeginResult?> BeginAsync(
            OpenIdConnectAuthBeginRequest request,
            IOpenIdConnectProviderResolver resolver,
            HttpContext httpContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(resolver);
            ArgumentNullException.ThrowIfNull(httpContext);

            if (string.IsNullOrWhiteSpace(request.ProviderKey))
            {
                return null;
            }

            OpenIdConnectProvider? provider = await resolver.FindProviderAsync(request.ProviderKey, cancellationToken);
            if (provider == null)
            {
                return null;
            }

            ValidateBegin(provider, request);

            OpenIdConnectConfiguration configuration = await _discovery.GetAsync(provider, cancellationToken);

            string codeVerifier = SecureToken.Generate();
            string nonce = request.Nonce ?? SecureToken.Generate();
            // max_age is whole seconds, so a fraction is dropped: the provider and completion then apply the same limit.
            long? maxAgeSeconds = request.MaxAuthenticationAge is TimeSpan maxAge ? (long)maxAge.TotalSeconds : null;

            FlowBinding binding = BindingCookie.Issue(httpContext, _stateLifetime);

            OpenIdConnectState state = new OpenIdConnectState
            {
                FlowId = binding.FlowId,
                BindingCookieHash = binding.ValueHash,
                CodeVerifier = codeVerifier,
                Nonce = nonce,
                ProviderKey = request.ProviderKey,
                Issuer = provider.Issuer.Value,
                ClientId = provider.ClientId,
                RedirectUri = request.RedirectUri,
                CallerBinding = request.CallerBinding,
                ExpiresAtUnixSeconds = (_timeProvider.GetUtcNow() + _stateLifetime).ToUnixTimeSeconds(),
                MaxAuthenticationAgeSeconds = maxAgeSeconds,
                AcceptedAcrValues = request.AcceptedAcrValues?.ToList(),
                AcceptedAmrValues = request.AcceptedAmrValues?.ToList(),
                RequiredClaimValues = request.RequiredClaimValues?.ToDictionary(StringComparer.Ordinal),
                ExpectedIdentity = request.ExpectedIdentity,
            };
            string protectedState = _stateSealer.Seal(state);

            Dictionary<string, string?> parameters = new Dictionary<string, string?>
            {
                ["client_id"] = provider.ClientId,
                ["response_type"] = "code",
                ["redirect_uri"] = request.RedirectUri,
                // Never form_post: a cross-site POST would not carry the SameSite=Lax binding cookie.
                ["response_mode"] = "query",
                ["scope"] = provider.Scope,
                ["state"] = protectedState,
                ["nonce"] = nonce,
                ["code_challenge"] = OAuthPkce.CreateS256Challenge(codeVerifier),
                ["code_challenge_method"] = "S256",
            };

            if (request.Prompt is OpenIdConnectPrompt prompt)
            {
                parameters["prompt"] = ToParameterValue(prompt);
            }

            if (request.LoginHint != null)
            {
                parameters["login_hint"] = request.LoginHint;
            }

            if (maxAgeSeconds is long seconds)
            {
                parameters["max_age"] = seconds.ToString(CultureInfo.InvariantCulture);
            }

            if (CreateClaimsRequest(request) is string claims)
            {
                parameters["claims"] = claims;
            }

            string authorizationUrl = QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, parameters);
            return new OpenIdConnectAuthBeginResult(authorizationUrl, protectedState);
        }

        /// <summary>
        /// Completes a flow from what the provider's redirect brought back: checks the state, the browser binding, the caller
        /// binding and where the response arrived, clears the flow's cookie, resolves the flow's provider again, checks the
        /// <c>iss</c> parameter, redeems the code, validates the ID token, and applies the rules the flow was begun with.
        /// </summary>
        /// <param name="response">The code, state and <c>iss</c> from the redirect, and the redirect URI it arrived at.</param>
        /// <param name="expectedCallerBinding">The caller binding the completing endpoint begins its own flows with. A flow
        /// begun with another is refused as <see cref="OpenIdConnectAuthFailureReason.CallerBindingMismatch"/> before its
        /// code is redeemed, so an endpoint never accepts a flow begun by another endpoint with other rules.</param>
        /// <param name="resolver">Resolves the provider key sealed in the state.</param>
        /// <param name="httpContext">The request from the browser completing the flow; its binding cookie is read from the
        /// request and cleared on the response.</param>
        /// <param name="cancellationToken">Cancels the calls to the provider.</param>
        /// <returns>The outcome, with the validated identity whenever the token itself validated.</returns>
        /// <exception cref="HttpRequestException">The provider's token endpoint could not be reached, or answered with an
        /// unsuccessful status other than 400 or 401 (a server error, or a 429 throttle). An outage is not a refusal, so it
        /// is not reported as one.</exception>
        /// <exception cref="InvalidOperationException">The provider's discovery document or keys could not be retrieved
        /// or are not ones it may use, or its token endpoint refused the client itself (an error
        /// other than <c>invalid_grant</c> or <c>invalid_request</c>, such as <c>invalid_client</c>).</exception>
        public async Task<OpenIdConnectCodeFlowResult> CompleteAsync(
            OpenIdConnectAuthorizationResponse response,
            string expectedCallerBinding,
            IOpenIdConnectProviderResolver resolver,
            HttpContext httpContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(expectedCallerBinding);
            ArgumentNullException.ThrowIfNull(resolver);
            ArgumentNullException.ThrowIfNull(httpContext);

            OpenIdConnectState? flow = _stateSealer.Open<OpenIdConnectState>(response.State);
            if (flow == null)
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.InvalidState);
            }

            // Read the binding before clearing it, and clear it whatever happens next: the code is single-use, so a flow
            // that reached completion cannot be completed again.
            FlowBinding binding = new FlowBinding(flow.FlowId, flow.BindingCookieHash);
            FlowBindingCookieCheck bindingCheck = BindingCookie.Check(httpContext, binding);
            BindingCookie.Clear(httpContext, binding);

            // Expiry first: an expired flow's cookie has expired with it, and the state is the accurate reason.
            if (_timeProvider.GetUtcNow().ToUnixTimeSeconds() >= flow.ExpiresAtUnixSeconds)
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.ExpiredState);
            }

            if (bindingCheck != FlowBindingCookieCheck.Matches)
            {
                return OpenIdConnectCodeFlowResult.Failed(bindingCheck == FlowBindingCookieCheck.Missing
                    ? OpenIdConnectAuthFailureReason.BindingCookieMissing
                    : OpenIdConnectAuthFailureReason.BindingCookieMismatch);
            }

            if (!string.Equals(flow.CallerBinding, expectedCallerBinding, StringComparison.Ordinal))
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.CallerBindingMismatch);
            }

            // Before anything goes to a provider: a response at another redirect URI may come from another provider, and
            // its code must not reach this flow's token endpoint.
            if (!string.Equals(response.RedirectUri, flow.RedirectUri, StringComparison.Ordinal))
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.RedirectUriMismatch);
            }

            if (string.IsNullOrEmpty(response.Code))
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.CodeRedemptionFailed);
            }

            OpenIdConnectProvider? provider = await resolver.FindProviderAsync(flow.ProviderKey, cancellationToken);
            if (provider == null)
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.UnknownProvider);
            }

            // A template's metadata address and tenant claim are not compared: only provider package code builds a
            // template, never a resolver's runtime data, and a fixed issuer's metadata address follows from its value.
            if (!string.Equals(provider.Issuer.Value, flow.Issuer, StringComparison.Ordinal)
                || !string.Equals(provider.ClientId, flow.ClientId, StringComparison.Ordinal))
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.ProviderChanged);
            }

            OpenIdConnectConfiguration configuration = await _discovery.GetAsync(provider, cancellationToken);
            bool isTemplate = provider.Issuer.IsTemplate;

            // RFC 9207. A template issuer is known only from the token, so its iss is compared once the token validated.
            if (response.Issuer == null && configuration.AuthorizationResponseIssParameterSupported)
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.IssuerParameterMissing);
            }

            if (response.Issuer != null && !isTemplate && !string.Equals(response.Issuer, provider.Issuer.Value, StringComparison.Ordinal))
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.IssuerParameterMismatch);
            }

            string? idToken = await _tokenEndpointClient.RedeemCodeAsync(provider, configuration.TokenEndpoint, response.Code, flow.RedirectUri, flow.CodeVerifier, cancellationToken);
            if (idToken == null)
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.CodeRedemptionFailed);
            }

            OpenIdConnectIdTokenValidationResult validation = await _idTokenValidator.ValidateAsync(flow.ProviderKey, provider, configuration, idToken, flow.Nonce, cancellationToken);
            if (!validation.IsValid)
            {
                return OpenIdConnectCodeFlowResult.Failed(validation.FailureReason.Value);
            }

            if (response.Issuer != null && isTemplate && !string.Equals(response.Issuer, validation.UserInfo.Identity.Issuer, StringComparison.Ordinal))
            {
                return OpenIdConnectCodeFlowResult.Failed(OpenIdConnectAuthFailureReason.IssuerParameterMismatch);
            }

            OpenIdConnectAuthFailureReason? refusal = CheckFlowRules(validation.UserInfo, flow);
            if (refusal is OpenIdConnectAuthFailureReason refusalReason)
            {
                return OpenIdConnectCodeFlowResult.Refused(refusalReason, validation.UserInfo);
            }

            return OpenIdConnectCodeFlowResult.Succeeded(validation.UserInfo, idToken, validation.SigningKey);
        }

        /// <summary>
        /// The rules a genuine token must also meet, the first broken one reported: the flow's expected identity, its
        /// accepted <c>acr</c> and <c>amr</c> values, its required claim values, and last its max authentication age, so
        /// a token that is both stale and of the wrong kind is reported for its kind, as Microsoft's authentication
        /// context always was.
        /// </summary>
        private OpenIdConnectAuthFailureReason? CheckFlowRules(OpenIdConnectUserInfo userInfo, OpenIdConnectState flow)
        {
            // The whole identity: with a multi-tenant provider the issuer names the tenant, so it is part of the account.
            if (flow.ExpectedIdentity is OpenIdConnectIdentity expected && userInfo.Identity != expected)
            {
                return OpenIdConnectAuthFailureReason.IdentityMismatch;
            }

            if (flow.AcceptedAcrValues != null)
            {
                if (userInfo.AuthenticationContextClass == null)
                {
                    return OpenIdConnectAuthFailureReason.MissingAcr;
                }

                if (!flow.AcceptedAcrValues.Contains(userInfo.AuthenticationContextClass, StringComparer.Ordinal))
                {
                    return OpenIdConnectAuthFailureReason.AcrNotAccepted;
                }
            }

            if (flow.AcceptedAmrValues != null)
            {
                if (userInfo.AuthenticationMethods.Count == 0)
                {
                    return OpenIdConnectAuthFailureReason.MissingAmr;
                }

                if (!userInfo.AuthenticationMethods.Intersect(flow.AcceptedAmrValues, StringComparer.Ordinal).Any())
                {
                    return OpenIdConnectAuthFailureReason.AmrNotAccepted;
                }
            }

            foreach (KeyValuePair<string, string> required in flow.RequiredClaimValues ?? NoRequiredClaims)
            {
                // The token's claims hold an array claim as one claim per element, so "equals or, as an array, holds".
                IReadOnlyList<string> values = userInfo.GetClaimValues(required.Key);
                if (values.Count == 0)
                {
                    return OpenIdConnectAuthFailureReason.MissingRequiredClaim;
                }

                if (!values.Contains(required.Value, StringComparer.Ordinal))
                {
                    return OpenIdConnectAuthFailureReason.RequiredClaimMismatch;
                }
            }

            if (flow.MaxAuthenticationAgeSeconds is long maxAgeSeconds)
            {
                if (userInfo.AuthenticationTime is not DateTimeOffset authenticationTime)
                {
                    return OpenIdConnectAuthFailureReason.MissingAuthTime;
                }

                if (_timeProvider.GetUtcNow() - authenticationTime > TimeSpan.FromSeconds(maxAgeSeconds))
                {
                    return OpenIdConnectAuthFailureReason.StaleAuthTime;
                }
            }

            return null;
        }

        private static void ValidateBegin(OpenIdConnectProvider provider, OpenIdConnectAuthBeginRequest request)
        {
            List<string> errors = new List<string>();

            if (request.RedirectUri == null || !provider.RedirectUris.Contains(request.RedirectUri, StringComparer.Ordinal))
            {
                errors.Add($"The redirect URI '{request.RedirectUri}' is not one of the provider's {nameof(OpenIdConnectProvider.RedirectUris)}.");
            }

            if (request.CallerBinding == null)
            {
                errors.Add($"{nameof(OpenIdConnectAuthBeginRequest.CallerBinding)} must not be null.");
            }

            if (request.Prompt is OpenIdConnectPrompt prompt && !Enum.IsDefined(prompt))
            {
                errors.Add($"{nameof(OpenIdConnectAuthBeginRequest.Prompt)} {(int)prompt} is not an {nameof(OpenIdConnectPrompt)} member.");
            }

            if (request.Nonce != null && string.IsNullOrWhiteSpace(request.Nonce))
            {
                errors.Add($"{nameof(OpenIdConnectAuthBeginRequest.Nonce)} must be null or non-blank.");
            }

            if (request.MaxAuthenticationAge is TimeSpan maxAge && maxAge < TimeSpan.FromSeconds(1))
            {
                errors.Add($"{nameof(OpenIdConnectAuthBeginRequest.MaxAuthenticationAge)} must be at least one second; got {maxAge}.");
            }

            AddValueListErrors(errors, request.AcceptedAcrValues, nameof(OpenIdConnectAuthBeginRequest.AcceptedAcrValues));
            AddValueListErrors(errors, request.AcceptedAmrValues, nameof(OpenIdConnectAuthBeginRequest.AcceptedAmrValues));

            if (request.ExpectedIdentity is OpenIdConnectIdentity expected && !provider.Issuer.CanIssueAs(expected.Issuer))
            {
                errors.Add($"{nameof(OpenIdConnectAuthBeginRequest.ExpectedIdentity)}'s issuer '{expected.Issuer}' cannot be the provider's issuer '{provider.Issuer}', so no sign-in could pass.");
            }

            foreach (KeyValuePair<string, string> claim in request.RequiredClaimValues ?? NoRequiredClaims)
            {
                if (string.IsNullOrWhiteSpace(claim.Key) || ReservedClaimRequests.Contains(claim.Key) || string.IsNullOrWhiteSpace(claim.Value))
                {
                    errors.Add($"The required claim '{claim.Key}' = '{claim.Value}' must have a non-blank name and value, and not be one of {string.Join(", ", ReservedClaimRequests)}, which the request's other rules ask for.");
                }
            }

            if (errors.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(OpenIdConnectAuthBeginRequest)}:\n{string.Join("\n", errors)}", nameof(request));
            }
        }

        private static void AddValueListErrors(List<string> errors, IReadOnlyList<string>? values, string name)
        {
            if (values != null && (values.Count == 0 || values.Any(string.IsNullOrWhiteSpace)))
            {
                errors.Add($"{name} must be null, or hold at least one value and no blank one.");
            }
        }

        /// <summary>
        /// The OIDC <c>claims</c> request for the flow's rules: <c>auth_time</c> for a max age, <c>acr</c> with the
        /// accepted values, <c>amr</c>, and each required claim with its value. Null when there is nothing to ask for.
        /// </summary>
        private static string? CreateClaimsRequest(OpenIdConnectAuthBeginRequest request)
        {
            JsonObject idTokenClaims = new JsonObject();

            if (request.MaxAuthenticationAge != null)
            {
                idTokenClaims["auth_time"] = new JsonObject { ["essential"] = true };
            }

            if (request.AcceptedAcrValues != null)
            {
                idTokenClaims["acr"] = new JsonObject
                {
                    ["essential"] = true,
                    ["values"] = new JsonArray(request.AcceptedAcrValues.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                };
            }

            if (request.AcceptedAmrValues != null)
            {
                idTokenClaims["amr"] = new JsonObject { ["essential"] = true };
            }

            foreach (KeyValuePair<string, string> claim in request.RequiredClaimValues ?? NoRequiredClaims)
            {
                idTokenClaims[claim.Key] = new JsonObject { ["essential"] = true, ["value"] = claim.Value };
            }

            return idTokenClaims.Count == 0 ? null : new JsonObject { ["id_token"] = idTokenClaims }.ToJsonString();
        }

        private static string ToParameterValue(OpenIdConnectPrompt prompt)
        {
            return prompt switch
            {
                OpenIdConnectPrompt.Login => "login",
                OpenIdConnectPrompt.None => "none",
                OpenIdConnectPrompt.Consent => "consent",
                OpenIdConnectPrompt.SelectAccount => "select_account",
                // ValidateBegin refuses an undefined member before anything is issued.
                _ => throw new UnreachableException($"Unhandled {nameof(OpenIdConnectPrompt)} {prompt}."),
            };
        }
    }
}
