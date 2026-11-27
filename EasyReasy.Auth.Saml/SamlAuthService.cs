using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The registered <see cref="ISamlAuthService"/> and the one home of the SAML sign-in flow: builds the AuthnRequest and
    /// binds it to the browser, checks a response at the assertion consumer service and keeps its outcome under a
    /// single-use code, and hands the outcome to the browser that began the sign-in. Scoped, with the application's resolver
    /// and store injected, so either may be scoped; the options and validator are singletons built at registration.
    /// </summary>
    internal sealed class SamlAuthService : ISamlAuthService
    {
        /// <summary>
        /// The name prefix of the binding cookie every SAML sign-in sets, so one browser holds at most
        /// <see cref="FlowBindingCookie.MaxPendingFlows"/> of them, apart from other kinds of flow.
        /// </summary>
        private const string BindingCookieNamePrefix = "__Host-easyreasy-saml-";

        private static readonly FlowBindingCookie BindingCookie = new FlowBindingCookie(BindingCookieNamePrefix);

        private readonly SamlAuthOptions _options;
        private readonly JsonSealer _sealer;
        private readonly SamlResponseValidator _validator;
        private readonly ISamlIdentityProviderResolver _resolver;
        private readonly ISamlAuthStore _store;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SamlAuthService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlAuthService"/> class.
        /// </summary>
        /// <param name="options">The validated options, copied at registration, whose state key seals what the store
        /// keeps.</param>
        /// <param name="validator">Checks a response against the options.</param>
        /// <param name="resolver">The application's resolver.</param>
        /// <param name="store">The application's store.</param>
        /// <param name="timeProvider">The clock every time is checked against.</param>
        /// <param name="logger">Records why a response was refused, for the operator.</param>
        public SamlAuthService(
            SamlAuthOptions options,
            SamlResponseValidator validator,
            ISamlIdentityProviderResolver resolver,
            ISamlAuthStore store,
            TimeProvider timeProvider,
            ILogger<SamlAuthService> logger)
        {
            _options = options;
            _sealer = new JsonSealer(options.StateKey);
            _validator = validator;
            _resolver = resolver;
            _store = store;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<SamlAuthBeginResult?> BeginAsync(SamlAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(httpContext);
            ThrowIfInvalid(request);

            SamlIdentityProvider? identityProvider = await _resolver.FindIdentityProviderAsync(request.IdentityProviderKey, cancellationToken);
            return identityProvider == null ? null : await BeginCheckedAsync(identityProvider, request, httpContext, cancellationToken);
        }

        /// <summary>
        /// Begins a sign-in with an identity provider the caller already resolved for
        /// <see cref="SamlAuthBeginRequest.IdentityProviderKey"/>, so a caller that needs the provider before it can build
        /// the request (the sign-in endpoint asks the handler only about a key that exists) resolves it once.
        /// </summary>
        /// <param name="identityProvider">What the application's resolver returned for the request's key.</param>
        /// <param name="request">The begin request.</param>
        /// <param name="httpContext">The request the binding cookie is set on.</param>
        /// <param name="cancellationToken">Cancels storing the request.</param>
        /// <returns>The URL to send the browser to.</returns>
        internal async Task<SamlAuthBeginResult> BeginWithResolvedAsync(SamlIdentityProvider identityProvider, SamlAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken)
        {
            ThrowIfInvalid(request);
            return await BeginCheckedAsync(identityProvider, request, httpContext, cancellationToken);
        }

        /// <summary>
        /// Begins a sign-in for a request its entry point checked with <see cref="ThrowIfInvalid"/>, each exactly once: the
        /// public begin before resolving, the sign-in endpoint after building the request from the handler's settings.
        /// </summary>
        private async Task<SamlAuthBeginResult> BeginCheckedAsync(SamlIdentityProvider identityProvider, SamlAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken)
        {
            if (request.ExpectedIdentity is SamlIdentity expected && expected.IdentityProviderEntityId != identityProvider.EntityId)
            {
                throw new ArgumentException(
                    $"{nameof(SamlAuthBeginRequest.ExpectedIdentity)} is at '{expected.IdentityProviderEntityId}', but the identity provider is '{identityProvider.EntityId}', so no sign-in could pass.",
                    nameof(request));
            }

            if (identityProvider.WantsSignedRequests && _options.SigningCertificate == null)
            {
                throw new InvalidOperationException(
                    $"The identity provider '{identityProvider.EntityId}' wants signed AuthnRequests, and {nameof(SamlAuthOptions)}.{nameof(SamlAuthOptions.SigningCertificate)} is not set.");
            }

            Saml2AuthnRequest authnRequest = CreateAuthnRequest(request, identityProvider);
            Saml2RedirectBinding binding = new Saml2RedirectBinding().Bind(authnRequest);

            DateTimeOffset now = _timeProvider.GetUtcNow();
            FlowBinding flowBinding = BindingCookie.Issue(httpContext, _options.RequestLifetime);
            SamlPendingState state = new SamlPendingState
            {
                FlowId = flowBinding.FlowId,
                BindingCookieHash = flowBinding.ValueHash,
                IdentityProviderKey = request.IdentityProviderKey,
                IdentityProviderEntityId = identityProvider.EntityId,
                CallerBinding = request.CallerBinding,
                CompletionPath = request.CompletionPath,
                MaxAuthenticationAge = request.MaxAuthenticationAge,
                AcceptedAuthnContextClassRefs = request.AcceptedAuthnContextClassRefs?.ToList(),
                ExpectedIdentity = request.ExpectedIdentity,
            };

            await _store.StorePendingRequestAsync(
                new SamlPendingRequestRecord(SecureToken.Hash(authnRequest.IdAsString), _sealer.Seal(state), (now + _options.RequestLifetime).UtcDateTime),
                cancellationToken);

            return new SamlAuthBeginResult(binding.RedirectLocation.OriginalString);
        }

        /// <inheritdoc />
        public async Task<SamlAuthCompletionResult> CompleteAsync(string code, string expectedCallerBinding, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedCallerBinding);
            ArgumentNullException.ThrowIfNull(httpContext);

            if (string.IsNullOrWhiteSpace(code))
            {
                return SamlAuthCompletionResult.Refused(SamlAuthFailureReason.UnknownCompletionCode, null);
            }

            string? sealedOutcome = await _store.ConsumeCompletionAsync(SecureToken.Hash(code), _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            SamlCompletionOutcome? outcome = sealedOutcome == null ? null : _sealer.Open<SamlCompletionOutcome>(sealedOutcome);
            if (outcome == null)
            {
                return SamlAuthCompletionResult.Refused(SamlAuthFailureReason.UnknownCompletionCode, null);
            }

            FlowBinding flowBinding = new FlowBinding(outcome.FlowId, outcome.BindingCookieHash);
            FlowBindingCookieCheck bindingCheck = BindingCookie.Check(httpContext, flowBinding);
            BindingCookie.Clear(httpContext, flowBinding);
            if (bindingCheck != FlowBindingCookieCheck.Matches)
            {
                SamlAuthFailureReason cookieReason = bindingCheck == FlowBindingCookieCheck.Missing
                    ? SamlAuthFailureReason.BindingCookieMissing
                    : SamlAuthFailureReason.BindingCookieMismatch;
                return SamlAuthCompletionResult.Refused(cookieReason, null);
            }

            if (!string.Equals(outcome.CallerBinding, expectedCallerBinding, StringComparison.Ordinal))
            {
                // Begun for another purpose, so it carried another caller's rules (or none): the outcome is not this one's.
                return SamlAuthCompletionResult.Refused(SamlAuthFailureReason.CallerBindingMismatch, outcome.AttemptedIdentity);
            }

            return outcome switch
            {
                SamlCompletionSuccess success => SamlAuthCompletionResult.Succeeded(new SamlUserInfo
                {
                    Identity = success.Identity,
                    AuthenticationInstant = success.AuthenticationInstant,
                    AuthnContextClassRef = success.AuthnContextClassRef,
                    Attributes = success.Attributes.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal),
                }),
                SamlCompletionRefusal refusal => SamlAuthCompletionResult.Refused(refusal.Reason, refusal.Identity),
                _ => throw new InvalidOperationException($"Unknown {nameof(SamlCompletionOutcome)} '{outcome.GetType().Name}'."),
            };
        }

        /// <summary>
        /// Checks a response posted to the assertion consumer service and keeps its outcome under a single-use code.
        /// </summary>
        /// <param name="samlResponse">The base64 <c>SAMLResponse</c> form value.</param>
        /// <param name="cancellationToken">Cancels the store and resolver calls.</param>
        /// <returns>The completion page path with the code to redirect the browser to, or null when the response answers
        /// no pending request this server began, which leaves nowhere to send the browser.</returns>
        internal async Task<string?> ConsumeResponseAsync(string samlResponse, CancellationToken cancellationToken)
        {
            string? claimedRequestId;
            try
            {
                claimedRequestId = SamlResponseValidator.ReadClaimedInResponseTo(samlResponse);
            }
            catch (SamlResponseRejectedException exception)
            {
                _logger.LogWarning("A SAML response was refused before its request was found: {Reason}", exception.Message);
                return null;
            }

            if (claimedRequestId == null)
            {
                _logger.LogWarning("A SAML response names no InResponseTo; unsolicited responses are refused.");
                return null;
            }

            // Consumed before anything else: whatever the response turns out to be, its request is answered once.
            string? sealedState = await _store.ConsumePendingRequestAsync(SecureToken.Hash(claimedRequestId), _timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            SamlPendingState? state = sealedState == null ? null : _sealer.Open<SamlPendingState>(sealedState);
            if (state == null)
            {
                _logger.LogWarning("A SAML response answers no pending request: unknown, already answered or expired.");
                return null;
            }

            SamlCompletionOutcome outcome = await EvaluateAsync(samlResponse, claimedRequestId, state, cancellationToken);

            string code = SecureToken.Generate();
            DateTimeOffset now = _timeProvider.GetUtcNow();
            await _store.StoreCompletionAsync(
                new SamlCompletionRecord(SecureToken.Hash(code), _sealer.Seal(outcome), (now + _options.CompletionLifetime).UtcDateTime),
                cancellationToken);

            string separator = state.CompletionPath.Contains('?') ? "&" : "?";
            return $"{state.CompletionPath}{separator}code={Uri.EscapeDataString(code)}";
        }

        private static void ThrowIfInvalid(SamlAuthBeginRequest request)
        {
            IReadOnlyList<string> problems = request.FindProblems();
            if (problems.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(SamlAuthBeginRequest)}:\n{string.Join("\n", problems)}", nameof(request));
            }
        }

        private Saml2AuthnRequest CreateAuthnRequest(SamlAuthBeginRequest request, SamlIdentityProvider identityProvider)
        {
            Saml2Configuration configuration = new Saml2Configuration
            {
                Issuer = _options.ServiceProviderEntityId,
                SingleSignOnDestination = identityProvider.SingleSignOnUrl,
                SigningCertificate = _options.SigningCertificate,
                SignAuthnRequest = _options.SigningCertificate != null,
            };

            Saml2AuthnRequest authnRequest = new Saml2AuthnRequest(configuration)
            {
                Destination = identityProvider.SingleSignOnUrl,
                AssertionConsumerServiceUrl = _options.AssertionConsumerServiceUrl,
                ProtocolBinding = ProtocolBindings.HttpPost,
                ForceAuthn = request.ForceAuthentication ? true : null,
            };

            if (identityProvider.IdentitySource.RequestedNameIdFormat is string nameIdFormat)
            {
                authnRequest.NameIdPolicy = new NameIdPolicy { AllowCreate = true, Format = nameIdFormat };
            }

            if (request.AcceptedAuthnContextClassRefs != null)
            {
                authnRequest.RequestedAuthnContext = new RequestedAuthnContext
                {
                    Comparison = AuthnContextComparisonTypes.Exact,
                    AuthnContextClassRef = request.AcceptedAuthnContextClassRefs.ToList(),
                };
            }

            return authnRequest;
        }

        private async Task<SamlCompletionOutcome> EvaluateAsync(string samlResponse, string requestId, SamlPendingState state, CancellationToken cancellationToken)
        {
            SamlIdentityProvider? identityProvider = await _resolver.FindIdentityProviderAsync(state.IdentityProviderKey, cancellationToken);
            if (identityProvider == null)
            {
                return Refuse(state, SamlAuthFailureReason.UnknownIdentityProvider, null);
            }

            if (identityProvider.EntityId != state.IdentityProviderEntityId)
            {
                return Refuse(state, SamlAuthFailureReason.IdentityProviderChanged, null);
            }

            SamlValidatedAssertion assertion;
            try
            {
                assertion = _validator.Validate(samlResponse, requestId, identityProvider);
            }
            catch (SamlResponseRejectedException exception)
            {
                _logger.LogWarning(exception, "A SAML response from '{IdentityProvider}' was refused: {Reason}", identityProvider.EntityId, exception.Message);
                return Refuse(state, SamlAuthFailureReason.InvalidResponse, null);
            }

            if (identityProvider.IdentitySource.ReadSubject(assertion, _options.ServiceProviderEntityId) is not string subject)
            {
                return Refuse(state, SamlAuthFailureReason.MissingIdentity, null);
            }

            SamlIdentity identity = new SamlIdentity(state.IdentityProviderKey, assertion.IdentityProviderEntityId, subject);
            SamlAuthFailureReason? ruleFailure = CheckRules(state, assertion, identity);
            if (ruleFailure is SamlAuthFailureReason reason)
            {
                return Refuse(state, reason, identity);
            }

            return new SamlCompletionSuccess
            {
                FlowId = state.FlowId,
                BindingCookieHash = state.BindingCookieHash,
                CallerBinding = state.CallerBinding,
                Identity = identity,
                AuthenticationInstant = assertion.AuthenticationInstant,
                AuthnContextClassRef = assertion.AuthnContextClassRef,
                Attributes = assertion.Attributes.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal),
            };
        }

        private SamlAuthFailureReason? CheckRules(SamlPendingState state, SamlValidatedAssertion assertion, SamlIdentity identity)
        {
            if (state.ExpectedIdentity is SamlIdentity expected && identity != expected)
            {
                return SamlAuthFailureReason.IdentityMismatch;
            }

            if (state.MaxAuthenticationAge is TimeSpan maxAge && _timeProvider.GetUtcNow() - assertion.AuthenticationInstant > maxAge)
            {
                return SamlAuthFailureReason.StaleAuthentication;
            }

            if (state.AcceptedAuthnContextClassRefs != null)
            {
                if (assertion.AuthnContextClassRef == null)
                {
                    return SamlAuthFailureReason.MissingAuthnContextClass;
                }

                if (!state.AcceptedAuthnContextClassRefs.Contains(assertion.AuthnContextClassRef, StringComparer.Ordinal))
                {
                    return SamlAuthFailureReason.AuthnContextClassNotAccepted;
                }
            }

            return null;
        }

        private static SamlCompletionRefusal Refuse(SamlPendingState state, SamlAuthFailureReason reason, SamlIdentity? attemptedIdentity)
        {
            return new SamlCompletionRefusal
            {
                FlowId = state.FlowId,
                BindingCookieHash = state.BindingCookieHash,
                CallerBinding = state.CallerBinding,
                Reason = reason,
                Identity = attemptedIdentity,
            };
        }
    }
}
