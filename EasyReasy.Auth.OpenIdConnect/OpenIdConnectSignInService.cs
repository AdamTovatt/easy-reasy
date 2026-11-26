using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Internal orchestrator behind the sign-in endpoints: begins a sign-in with the provider the browser chose and the
    /// rules the consumer's <see cref="IOpenIdConnectAuthHandler"/> decides, and completes one by delegating the verified
    /// identity to that handler, surfacing the outcome as an <see cref="ExternalAuthResult"/> for audit logging.
    /// </summary>
    internal sealed class OpenIdConnectSignInService
    {
        /// <summary>
        /// The provider name recorded on every <see cref="ExternalAuthResult"/> this service produces. Which provider is
        /// in the attempted subject's issuer.
        /// </summary>
        internal const string ProviderName = "oidc";

        /// <summary>
        /// The caller binding every sign-in begun here carries, and the only one the sign-in endpoint accepts. A state an
        /// application began for its own flow (linking an account, a re-check at signing) carries the application's
        /// binding instead, so posting it to the sign-in endpoint cannot sign anyone in.
        /// </summary>
        internal const string SignInCallerBinding = "easyreasy.auth.openidconnect:sign-in";

        private readonly IOpenIdConnectAuthService _authService;
        private readonly IOpenIdConnectAuthHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectSignInService"/> class.
        /// </summary>
        /// <param name="authService">The OpenID Connect auth service for beginning and completing the flow.</param>
        /// <param name="handler">The consumer-provided handler for the sign-in's rules and the verified user.</param>
        public OpenIdConnectSignInService(IOpenIdConnectAuthService authService, IOpenIdConnectAuthHandler handler)
        {
            _authService = authService;
            _handler = handler;
        }

        /// <summary>
        /// Begins a sign-in with the provider the browser chose, the handler's settings and the browser's login hint.
        /// </summary>
        /// <param name="providerKey">The provider key from the browser.</param>
        /// <param name="loginHint">The login hint from the browser, or null. A blank one, or one longer than
        /// <see cref="OpenIdConnectLoginHint.MaxLength"/>, is not sent.</param>
        /// <param name="httpContext">The begin request, which the binding cookie is set on.</param>
        /// <returns>Where to send the browser, or null when the provider key is blank, not offered for sign-in by the
        /// handler, or resolves to no provider.</returns>
        /// <exception cref="ArgumentException">The handler's settings are refused by
        /// <see cref="IOpenIdConnectAuthService.BeginAsync"/>.</exception>
        public async Task<OpenIdConnectSignInBeginResponse?> BeginAsync(string? providerKey, string? loginHint, HttpContext httpContext)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return null;
            }

            OpenIdConnectSignInSettings? settings = await _handler.GetSignInSettingsAsync(providerKey, httpContext);
            if (settings == null)
            {
                return null;
            }

            OpenIdConnectAuthBeginResult? begun = await _authService.BeginAsync(new OpenIdConnectAuthBeginRequest
            {
                ProviderKey = providerKey,
                RedirectUri = settings.RedirectUri,
                CallerBinding = SignInCallerBinding,
                Prompt = settings.Prompt,
                LoginHint = OpenIdConnectLoginHint.FromBrowser(loginHint),
                MaxAuthenticationAge = settings.MaxAuthenticationAge,
                AcceptedAcrValues = settings.AcceptedAcrValues,
                AcceptedAmrValues = settings.AcceptedAmrValues,
                RequiredClaimValues = settings.RequiredClaimValues,
            }, httpContext, httpContext.RequestAborted);

            return begun == null ? null : new OpenIdConnectSignInBeginResponse(begun.AuthorizationUrl);
        }

        /// <summary>
        /// Completes a sign-in and delegates the verified identity to the handler.
        /// </summary>
        /// <param name="response">What the provider's redirect brought back, as the redirect URI page posted it.</param>
        /// <param name="jwtTokenService">The JWT token service for creating access tokens.</param>
        /// <param name="refreshTokenService">The refresh token service, or null if not configured.</param>
        /// <param name="httpContext">The sign-in request, whose binding cookie is checked and cleared.</param>
        /// <returns>
        /// Success with the issued <see cref="AuthResponse"/>, or a failure whose
        /// <see cref="ExternalAuthResult.FailureDetail"/> names the <see cref="OpenIdConnectAuthFailureReason"/>. A handler
        /// that returns null gives <see cref="ExternalAuthFailureReason.Rejected"/> with no detail.
        /// </returns>
        /// <exception cref="HttpRequestException">The provider's token endpoint could not be reached or failed.</exception>
        /// <exception cref="InvalidOperationException">The provider is invalid, or its discovery document or keys could not
        /// be retrieved.</exception>
        public async Task<ExternalAuthResult> AuthenticateAsync(
            OpenIdConnectAuthorizationResponse response,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext httpContext)
        {
            OpenIdConnectAuthCompletionResult completion = await _authService.CompleteAsync(response, SignInCallerBinding, httpContext, httpContext.RequestAborted);
            string? attemptedSubject = completion.AttemptedIdentity?.ToString();

            if (!completion.Success)
            {
                return Refuse(completion.FailureReason.Value, attemptedSubject);
            }

            AuthResponse? authResponse = await _handler.HandleOpenIdConnectUserAsync(completion.UserInfo, jwtTokenService, refreshTokenService, httpContext);

            if (authResponse == null)
            {
                return ExternalAuthResult.Failed(ProviderName, ExternalAuthFailureReason.Rejected, attemptedSubject);
            }

            return ExternalAuthResult.Succeeded(ProviderName, authResponse, completion.AttemptedIdentity.Value.ToString());
        }

        private static ExternalAuthResult Refuse(OpenIdConnectAuthFailureReason reason, string? attemptedSubject)
        {
            return ExternalAuthResult.Failed(ProviderName, reason.ToExternalAuthFailureReason(), attemptedSubject, reason.ToString());
        }
    }
}
