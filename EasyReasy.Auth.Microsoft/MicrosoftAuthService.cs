using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The registered <see cref="IMicrosoftAuthService"/>.
    /// </summary>
    internal sealed class MicrosoftAuthService : IMicrosoftAuthService
    {
        private const int FlowIdSize = 16;
        private const int CodeVerifierSize = 32;
        private const int GeneratedNonceSize = 32;

        private readonly MicrosoftAuthOptions _options;
        private readonly MicrosoftAuthority _authority;
        private readonly MicrosoftAuthStateProtector _stateProtector;
        private readonly MicrosoftTokenEndpointClient _tokenEndpointClient;
        private readonly MicrosoftIdTokenValidator _idTokenValidator;
        private readonly TimeProvider _timeProvider;
        private readonly HashSet<Guid> _allowedTenantIds;

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftAuthService"/> class.
        /// </summary>
        public MicrosoftAuthService(
            MicrosoftAuthOptions options,
            MicrosoftAuthority authority,
            MicrosoftAuthStateProtector stateProtector,
            MicrosoftTokenEndpointClient tokenEndpointClient,
            MicrosoftIdTokenValidator idTokenValidator,
            TimeProvider timeProvider)
        {
            _options = options;
            _authority = authority;
            _stateProtector = stateProtector;
            _tokenEndpointClient = tokenEndpointClient;
            _idTokenValidator = idTokenValidator;
            _timeProvider = timeProvider;
            _allowedTenantIds = (options.AllowedTenantIds ?? []).ToHashSet();
        }

        /// <inheritdoc />
        public MicrosoftAuthBeginResult Begin(MicrosoftAuthBeginRequest request, HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(httpContext);
            ValidateBeginRequest(request);

            string flowId = MicrosoftRandomValue.Create(FlowIdSize);
            string codeVerifier = MicrosoftRandomValue.Create(CodeVerifierSize);
            string nonce = request.Nonce ?? MicrosoftRandomValue.Create(GeneratedNonceSize);
            // max_age is whole seconds, so a fraction is dropped: Microsoft and Complete then apply the same limit.
            long? maxAgeSeconds = request.MaxAuthenticationAge is TimeSpan maxAge ? (long)maxAge.TotalSeconds : null;

            string bindingCookieHash = MicrosoftBindingCookie.Issue(httpContext, flowId, _options.StateLifetime);

            MicrosoftAuthState state = new MicrosoftAuthState
            {
                FlowId = flowId,
                BindingCookieHash = bindingCookieHash,
                CodeVerifier = codeVerifier,
                Nonce = nonce,
                RequiredTenantId = request.TenantId,
                RequiredAuthenticationContextId = request.RequiredAuthenticationContextId,
                MaxAuthenticationAgeSeconds = maxAgeSeconds,
                RedirectUri = request.RedirectUri,
                CallerBinding = request.CallerBinding,
                ExpiresAtUnixSeconds = (_timeProvider.GetUtcNow() + _options.StateLifetime).ToUnixTimeSeconds(),
            };
            string protectedState = _stateProtector.Protect(state);

            Dictionary<string, string?> parameters = new Dictionary<string, string?>
            {
                ["client_id"] = _options.ClientId,
                ["response_type"] = "code",
                ["redirect_uri"] = request.RedirectUri,
                // Never form_post: a cross-site POST would not carry the SameSite=Lax binding cookie.
                ["response_mode"] = "query",
                ["scope"] = MicrosoftAuthority.Scope,
                ["state"] = protectedState,
                ["nonce"] = nonce,
                ["code_challenge"] = CreateCodeChallenge(codeVerifier),
                ["code_challenge_method"] = "S256",
            };

            if (request.Prompt is MicrosoftAuthPrompt prompt)
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

            if (request.RequiredAuthenticationContextId != null)
            {
                parameters["claims"] = CreateAuthenticationContextClaimsRequest(request.RequiredAuthenticationContextId);
            }

            string authorizationUrl = QueryHelpers.AddQueryString(_authority.GetAuthorizeEndpoint(state.RequiredTenantId), parameters);
            return new MicrosoftAuthBeginResult(authorizationUrl, protectedState);
        }

        /// <inheritdoc />
        public async Task<MicrosoftAuthCompletionResult> CompleteAsync(string code, string state, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            MicrosoftAuthState? flow = _stateProtector.Unprotect(state);
            if (flow == null)
            {
                return MicrosoftAuthCompletionResult.Failed(MicrosoftAuthFailureReason.InvalidState);
            }

            // Read the binding before clearing it, and clear it whatever happens next: the code is single-use, so a
            // flow that reached Complete cannot be completed again.
            MicrosoftAuthFailureReason? bindingFailure = MicrosoftBindingCookie.Check(httpContext, flow.FlowId, flow.BindingCookieHash);
            MicrosoftBindingCookie.Clear(httpContext, flow.FlowId);

            // Expiry first: an expired flow's cookie has expired with it, and the state is the accurate reason.
            if (_timeProvider.GetUtcNow().ToUnixTimeSeconds() >= flow.ExpiresAtUnixSeconds)
            {
                return MicrosoftAuthCompletionResult.Failed(MicrosoftAuthFailureReason.ExpiredState);
            }

            if (bindingFailure is MicrosoftAuthFailureReason failure)
            {
                return MicrosoftAuthCompletionResult.Failed(failure);
            }

            if (string.IsNullOrEmpty(code))
            {
                return MicrosoftAuthCompletionResult.Failed(MicrosoftAuthFailureReason.CodeRedemptionFailed);
            }

            string? idToken = await _tokenEndpointClient.RedeemCodeAsync(flow.RequiredTenantId, code, flow.RedirectUri, flow.CodeVerifier, cancellationToken);
            if (idToken == null)
            {
                return MicrosoftAuthCompletionResult.Failed(MicrosoftAuthFailureReason.CodeRedemptionFailed);
            }

            MicrosoftIdTokenValidationResult validation = await _idTokenValidator.ValidateAsync(idToken, flow.Nonce, cancellationToken);
            if (!validation.IsValid)
            {
                return MicrosoftAuthCompletionResult.Failed(validation.FailureReason.Value);
            }

            MicrosoftAuthFailureReason? refusal = CheckFlowRules(validation.UserInfo, flow);
            if (refusal is MicrosoftAuthFailureReason refusalReason)
            {
                return MicrosoftAuthCompletionResult.Refused(refusalReason, validation.UserInfo);
            }

            return MicrosoftAuthCompletionResult.Succeeded(validation.UserInfo, flow.CallerBinding);
        }

        /// <summary>
        /// The rules a genuine token must also meet: no personal account, the flow's tenant, the allowed tenants,
        /// the flow's authentication context and its max authentication age.
        /// </summary>
        private MicrosoftAuthFailureReason? CheckFlowRules(MicrosoftUserInfo userInfo, MicrosoftAuthState flow)
        {
            Guid tenantId = userInfo.TenantId;

            if (tenantId == MicrosoftAuthority.PersonalAccountsTenantId)
            {
                return MicrosoftAuthFailureReason.PersonalAccount;
            }

            if (flow.RequiredTenantId is Guid requiredTenantId && tenantId != requiredTenantId)
            {
                return MicrosoftAuthFailureReason.TenantMismatch;
            }

            if (_allowedTenantIds.Count > 0 && !_allowedTenantIds.Contains(tenantId))
            {
                return MicrosoftAuthFailureReason.TenantNotAllowed;
            }

            if (flow.RequiredAuthenticationContextId != null
                && !userInfo.AuthenticationContextIds.Contains(flow.RequiredAuthenticationContextId, StringComparer.Ordinal))
            {
                return MicrosoftAuthFailureReason.MissingAuthenticationContext;
            }

            if (flow.MaxAuthenticationAgeSeconds is long maxAgeSeconds)
            {
                if (userInfo.AuthenticationTime is not DateTimeOffset authenticationTime)
                {
                    return MicrosoftAuthFailureReason.MissingAuthTime;
                }

                if (_timeProvider.GetUtcNow() - authenticationTime > TimeSpan.FromSeconds(maxAgeSeconds))
                {
                    return MicrosoftAuthFailureReason.StaleAuthTime;
                }
            }

            return null;
        }

        private void ValidateBeginRequest(MicrosoftAuthBeginRequest request)
        {
            List<string> errors = new List<string>();

            if (request.RedirectUri == null || !_options.RedirectUris.Contains(request.RedirectUri, StringComparer.Ordinal))
            {
                errors.Add($"The redirect URI '{request.RedirectUri}' is not one of {nameof(MicrosoftAuthOptions)}.{nameof(MicrosoftAuthOptions.RedirectUris)}.");
            }

            if (request.CallerBinding == null)
            {
                errors.Add($"{nameof(MicrosoftAuthBeginRequest.CallerBinding)} must not be null.");
            }

            if (request.TenantId == MicrosoftAuthority.PersonalAccountsTenantId)
            {
                errors.Add("The tenant id is the personal Microsoft accounts tenant, whose accounts are always refused.");
            }

            if (request.Prompt is MicrosoftAuthPrompt prompt && !Enum.IsDefined(prompt))
            {
                errors.Add($"{nameof(MicrosoftAuthBeginRequest.Prompt)} {(int)prompt} is not a {nameof(MicrosoftAuthPrompt)} member.");
            }

            if (request.Nonce != null && string.IsNullOrWhiteSpace(request.Nonce))
            {
                errors.Add($"{nameof(MicrosoftAuthBeginRequest.Nonce)} must be null or non-blank.");
            }

            if (request.RequiredAuthenticationContextId != null && string.IsNullOrWhiteSpace(request.RequiredAuthenticationContextId))
            {
                errors.Add($"{nameof(MicrosoftAuthBeginRequest.RequiredAuthenticationContextId)} must be null or non-blank.");
            }

            if (request.MaxAuthenticationAge is TimeSpan maxAge && maxAge < TimeSpan.FromSeconds(1))
            {
                errors.Add($"{nameof(MicrosoftAuthBeginRequest.MaxAuthenticationAge)} must be at least one second; got {maxAge}.");
            }

            if (errors.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(MicrosoftAuthBeginRequest)}:\n{string.Join("\n", errors)}", nameof(request));
            }
        }

        /// <summary>
        /// The OIDC <c>claims</c> request for an authentication context, as Conditional Access expects it.
        /// </summary>
        private static string CreateAuthenticationContextClaimsRequest(string authenticationContextId)
        {
            JsonObject claims = new JsonObject
            {
                ["id_token"] = new JsonObject
                {
                    ["acrs"] = new JsonObject
                    {
                        ["essential"] = true,
                        ["value"] = authenticationContextId,
                    },
                },
            };

            return claims.ToJsonString();
        }

        private static string ToParameterValue(MicrosoftAuthPrompt prompt)
        {
            return prompt switch
            {
                MicrosoftAuthPrompt.Login => "login",
                MicrosoftAuthPrompt.None => "none",
                MicrosoftAuthPrompt.Consent => "consent",
                MicrosoftAuthPrompt.SelectAccount => "select_account",
                // ValidateBeginRequest refuses an undefined member before anything is issued.
                _ => throw new UnreachableException($"Unhandled {nameof(MicrosoftAuthPrompt)} {prompt}."),
            };
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            return Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }
    }
}
