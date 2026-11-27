using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Internal orchestrator behind the sign-in endpoints, built on the registered <see cref="SamlAuthService"/>: begins a
    /// sign-in with the identity provider the browser chose and the rules the consumer's <see cref="ISamlAuthHandler"/>
    /// decides, and completes one by delegating the verified user to that handler, surfacing the outcome as an
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
        private readonly ISamlAuthHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlSignInService"/> class.
        /// </summary>
        /// <param name="authService">The application's SAML auth service.</param>
        /// <param name="handler">The consumer-provided handler for the sign-in's rules and the verified user.</param>
        public SamlSignInService(ISamlAuthService authService, ISamlAuthHandler handler)
        {
            _authService = authService;
            _handler = handler;
        }

        /// <summary>
        /// Begins a sign-in with the identity provider the browser chose and the handler's settings.
        /// </summary>
        /// <param name="identityProviderKey">The identity provider key from the browser.</param>
        /// <param name="httpContext">The begin request, which the binding cookie is set on.</param>
        /// <returns>Where to send the browser, or null when the key is blank, not offered for sign-in by the handler, or
        /// resolves to no identity provider.</returns>
        /// <exception cref="ArgumentException">The handler's settings are refused by
        /// <see cref="ISamlAuthService.BeginAsync"/>.</exception>
        public async Task<SamlSignInBeginResponse?> BeginAsync(string? identityProviderKey, HttpContext httpContext)
        {
            if (string.IsNullOrWhiteSpace(identityProviderKey))
            {
                return null;
            }

            SamlSignInSettings? settings = await _handler.GetSignInSettingsAsync(identityProviderKey, httpContext);
            if (settings == null)
            {
                return null;
            }

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
            SamlAuthCompletionResult completion = await _authService.CompleteAsync(code, SignInCallerBinding, httpContext, httpContext.RequestAborted);
            string? attemptedSubject = completion.AttemptedIdentity?.ToString();

            if (!completion.Success)
            {
                return Refuse(completion.FailureReason.Value, attemptedSubject);
            }

            AuthResponse? authResponse = await _handler.HandleSamlUserAsync(completion.UserInfo, jwtTokenService, refreshTokenService, httpContext);

            if (authResponse == null)
            {
                return ExternalAuthResult.Failed(ProviderName, ExternalAuthFailureReason.Rejected, attemptedSubject);
            }

            return ExternalAuthResult.Succeeded(ProviderName, authResponse, completion.AttemptedIdentity.Value.ToString());
        }

        private static ExternalAuthResult Refuse(SamlAuthFailureReason reason, string? attemptedSubject)
        {
            return ExternalAuthResult.Failed(ProviderName, reason.ToExternalAuthFailureReason(), attemptedSubject, reason.ToString());
        }
    }
}
