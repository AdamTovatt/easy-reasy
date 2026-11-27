using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Internal orchestrator behind the sign-in endpoints, built on <see cref="ISamlAuthService"/>: begins a sign-in with
    /// the identity provider the browser chose and the rules the consumer's <see cref="ISamlAuthHandler"/> decides, and
    /// completes one by delegating the verified user to that handler, surfacing the outcome as an
    /// <see cref="ExternalAuthResult"/> for audit logging.
    /// </summary>
    internal sealed class SamlSignInService
    {
        /// <summary>
        /// The provider name recorded on every <see cref="ExternalAuthResult"/> this service produces. Which identity
        /// provider is in the attempted subject.
        /// </summary>
        internal const string ProviderName = "saml";

        /// <summary>
        /// The caller binding every sign-in begun here carries, and the only one the sign-in endpoint accepts. A sign-in an
        /// application began for its own flow (linking an account, a re-check at signing) carries the application's binding
        /// instead, so posting its code to the sign-in endpoint cannot sign anyone in.
        /// </summary>
        internal const string SignInCallerBinding = "easyreasy.auth.saml:sign-in";

        private readonly ISamlAuthService _authService;
        private readonly ISamlIdentityProviderResolver _resolver;
        private readonly ISamlAuthHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlSignInService"/> class.
        /// </summary>
        /// <param name="authService">The application's SAML auth service.</param>
        /// <param name="resolver">The application's resolver, asked first so the handler is only asked about an identity
        /// provider that exists.</param>
        /// <param name="handler">The consumer-provided handler for the sign-in's rules and the verified user.</param>
        public SamlSignInService(ISamlAuthService authService, ISamlIdentityProviderResolver resolver, ISamlAuthHandler handler)
        {
            _authService = authService;
            _resolver = resolver;
            _handler = handler;
        }

        /// <summary>
        /// Begins a sign-in with the identity provider the browser chose and the handler's settings.
        /// </summary>
        /// <param name="identityProviderKey">The identity provider key from the browser.</param>
        /// <param name="httpContext">The begin request, which the binding cookie is set on.</param>
        /// <returns>Where to send the browser, or null when the key resolves to no identity provider.</returns>
        /// <exception cref="ArgumentException">The handler's settings are refused by
        /// <see cref="ISamlAuthService.BeginAsync"/>.</exception>
        public async Task<SamlSignInBeginResponse?> BeginAsync(string? identityProviderKey, HttpContext httpContext)
        {
            if (string.IsNullOrWhiteSpace(identityProviderKey))
            {
                return null;
            }

            // The handler is only asked about an identity provider that exists, never about a key the browser made up;
            // the auth service resolves it again, and a key that vanished in between gives null there too.
            if (await _resolver.FindIdentityProviderAsync(identityProviderKey, httpContext.RequestAborted) == null)
            {
                return null;
            }

            SamlSignInSettings settings = await _handler.GetSignInSettingsAsync(identityProviderKey, httpContext);
            SamlAuthBeginResult? begun = await _authService.BeginAsync(new SamlAuthBeginRequest
            {
                IdentityProviderKey = identityProviderKey,
                CallerBinding = SignInCallerBinding,
                CompletionPath = settings.CompletionPath,
                ForceAuthentication = settings.ForceAuthentication,
                MaxAuthenticationAge = settings.MaxAuthenticationAge,
                AcceptedAuthnContextClassRefs = settings.AcceptedAuthnContextClassRefs,
            }, httpContext, httpContext.RequestAborted);

            return begun == null ? null : new SamlSignInBeginResponse(begun.RedirectUrl);
        }

        /// <summary>
        /// Completes a sign-in and delegates the verified user to the handler.
        /// </summary>
        /// <param name="code">The completion code, as the completion page posted it.</param>
        /// <param name="jwtTokenService">The JWT token service for creating access tokens.</param>
        /// <param name="refreshTokenService">The refresh token service, or null if not configured.</param>
        /// <param name="httpContext">The sign-in request, whose binding cookie is checked and cleared.</param>
        /// <returns>
        /// Success with the issued <see cref="AuthResponse"/>, or a failure whose
        /// <see cref="ExternalAuthResult.FailureDetail"/> names the <see cref="SamlAuthFailureReason"/>. A handler that
        /// returns null gives <see cref="ExternalAuthFailureReason.Rejected"/> with no detail.
        /// </returns>
        public async Task<ExternalAuthResult> AuthenticateAsync(
            string code,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext httpContext)
        {
            SamlAuthCompletionResult completion = await _authService.CompleteAsync(code, httpContext, httpContext.RequestAborted);
            string? attemptedSubject = completion.AttemptedIdentity?.ToString();

            if (!completion.Success)
            {
                return Refuse(completion.FailureReason.Value, attemptedSubject);
            }

            if (!string.Equals(completion.CallerBinding, SignInCallerBinding, StringComparison.Ordinal))
            {
                return Refuse(SamlAuthFailureReason.CallerBindingMismatch, attemptedSubject);
            }

            AuthResponse? authResponse = await _handler.HandleSamlUserAsync(completion.UserInfo, jwtTokenService, refreshTokenService, httpContext);

            if (authResponse == null)
            {
                return ExternalAuthResult.Failed(ProviderName, ExternalAuthFailureReason.Rejected, attemptedSubject);
            }

            return ExternalAuthResult.Succeeded(ProviderName, authResponse, completion.AttemptedIdentity.Value.ToString());
        }

        /// <summary>
        /// The provider-neutral category of a refusal: a failure of the code, the binding cookie, the identity provider or
        /// the response is <see cref="ExternalAuthFailureReason.InvalidToken"/>; a verified assertion refused by the
        /// sign-in's rules is <see cref="ExternalAuthFailureReason.Rejected"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A reason with no category, so a new member cannot ship
        /// unclassified.</exception>
        internal static ExternalAuthFailureReason GetCategory(SamlAuthFailureReason reason)
        {
            return reason switch
            {
                SamlAuthFailureReason.UnknownCompletionCode
                    or SamlAuthFailureReason.BindingCookieMissing
                    or SamlAuthFailureReason.BindingCookieMismatch
                    or SamlAuthFailureReason.UnknownIdentityProvider
                    or SamlAuthFailureReason.IdentityProviderChanged
                    or SamlAuthFailureReason.InvalidResponse
                    or SamlAuthFailureReason.CallerBindingMismatch => ExternalAuthFailureReason.InvalidToken,
                SamlAuthFailureReason.MissingIdentity
                    or SamlAuthFailureReason.IdentityMismatch
                    or SamlAuthFailureReason.StaleAuthentication
                    or SamlAuthFailureReason.MissingAuthnContextClass
                    or SamlAuthFailureReason.AuthnContextClassNotAccepted => ExternalAuthFailureReason.Rejected,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "The refusal reason has no category."),
            };
        }

        private static ExternalAuthResult Refuse(SamlAuthFailureReason reason, string? attemptedSubject)
        {
            return ExternalAuthResult.Failed(ProviderName, GetCategory(reason), attemptedSubject, reason.ToString());
        }
    }
}
