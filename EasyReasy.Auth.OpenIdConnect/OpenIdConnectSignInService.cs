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

        /// <summary>
        /// The longest login hint the begin endpoint passes on; a longer one is ignored. Longer than any sign-in name (an
        /// email address is at most 254 characters), so it only stops an anonymous caller from having an arbitrarily large
        /// authorize URL built and returned.
        /// </summary>
        internal const int MaxLoginHintLength = 256;

        private readonly OpenIdConnectCodeFlow _flow;
        private readonly IOpenIdConnectProviderResolver _resolver;
        private readonly IOpenIdConnectAuthHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectSignInService"/> class.
        /// </summary>
        /// <param name="flow">The application's flow.</param>
        /// <param name="resolver">The consumer-provided resolver of the providers.</param>
        /// <param name="handler">The consumer-provided handler for the sign-in's rules and the verified user.</param>
        public OpenIdConnectSignInService(OpenIdConnectCodeFlow flow, IOpenIdConnectProviderResolver resolver, IOpenIdConnectAuthHandler handler)
        {
            _flow = flow;
            _resolver = resolver;
            _handler = handler;
        }

        /// <summary>
        /// Begins a sign-in with the provider the browser chose, the handler's settings and the browser's login hint.
        /// </summary>
        /// <param name="providerKey">The provider key from the browser.</param>
        /// <param name="loginHint">The login hint from the browser, or null. A blank one, or one longer than
        /// <see cref="MaxLoginHintLength"/>, is not sent.</param>
        /// <param name="httpContext">The begin request, which the binding cookie is set on.</param>
        /// <returns>Where to send the browser, or null when the provider key resolves to no provider.</returns>
        /// <exception cref="ArgumentException">The handler's settings are refused by
        /// <see cref="OpenIdConnectCodeFlow.BeginAsync"/>.</exception>
        public async Task<OpenIdConnectSignInBeginResponse?> BeginAsync(string? providerKey, string? loginHint, HttpContext httpContext)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return null;
            }

            OpenIdConnectProvider? provider = await _resolver.FindProviderAsync(providerKey, httpContext.RequestAborted);
            if (provider == null)
            {
                return null;
            }

            OpenIdConnectSignInSettings settings = await _handler.GetSignInSettingsAsync(providerKey, httpContext);

            OpenIdConnectAuthBeginResult begun = await _flow.BeginAsync(provider, new OpenIdConnectAuthBeginRequest
            {
                ProviderKey = providerKey,
                RedirectUri = settings.RedirectUri,
                CallerBinding = SignInCallerBinding,
                Prompt = settings.Prompt,
                LoginHint = string.IsNullOrWhiteSpace(loginHint) || loginHint.Length > MaxLoginHintLength ? null : loginHint,
                MaxAuthenticationAge = settings.MaxAuthenticationAge,
                AcceptedAcrValues = settings.AcceptedAcrValues,
                AcceptedAmrValues = settings.AcceptedAmrValues,
            }, httpContext, cancellationToken: httpContext.RequestAborted);

            return new OpenIdConnectSignInBeginResponse(begun.AuthorizationUrl);
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
            OpenIdConnectAuthCompletionResult completion = OpenIdConnectAuthCompletionResult.From(
                await _flow.CompleteAsync(response, _resolver, httpContext, httpContext.RequestAborted));
            string? attemptedSubject = completion.AttemptedIdentity?.ToString();

            if (!completion.Success)
            {
                return Refuse(completion.FailureReason.Value, attemptedSubject);
            }

            if (!string.Equals(completion.CallerBinding, SignInCallerBinding, StringComparison.Ordinal))
            {
                return Refuse(OpenIdConnectAuthFailureReason.CallerBindingMismatch, attemptedSubject);
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
            return ExternalAuthResult.Failed(ProviderName, GetCategory(reason), attemptedSubject, reason.ToString());
        }

        /// <summary>
        /// The provider-neutral category of a refusal: a failure of the state, the binding cookie, the response, the
        /// provider, the code or the token is <see cref="ExternalAuthFailureReason.InvalidToken"/>; a genuine identity
        /// refused by the flow's rules is <see cref="ExternalAuthFailureReason.Rejected"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A reason with no category, so a new member cannot ship
        /// unclassified.</exception>
        internal static ExternalAuthFailureReason GetCategory(OpenIdConnectAuthFailureReason reason)
        {
            return reason switch
            {
                OpenIdConnectAuthFailureReason.InvalidState
                    or OpenIdConnectAuthFailureReason.ExpiredState
                    or OpenIdConnectAuthFailureReason.BindingCookieMissing
                    or OpenIdConnectAuthFailureReason.BindingCookieMismatch
                    or OpenIdConnectAuthFailureReason.RedirectUriMismatch
                    or OpenIdConnectAuthFailureReason.IssuerParameterMismatch
                    or OpenIdConnectAuthFailureReason.IssuerParameterMissing
                    or OpenIdConnectAuthFailureReason.UnknownProvider
                    or OpenIdConnectAuthFailureReason.ProviderChanged
                    or OpenIdConnectAuthFailureReason.CodeRedemptionFailed
                    or OpenIdConnectAuthFailureReason.InvalidToken
                    or OpenIdConnectAuthFailureReason.InvalidSignature
                    or OpenIdConnectAuthFailureReason.InvalidAudience
                    or OpenIdConnectAuthFailureReason.InvalidIssuer
                    or OpenIdConnectAuthFailureReason.TokenExpired
                    or OpenIdConnectAuthFailureReason.NonceMismatch
                    or OpenIdConnectAuthFailureReason.CallerBindingMismatch => ExternalAuthFailureReason.InvalidToken,
                OpenIdConnectAuthFailureReason.IdentityMismatch
                    or OpenIdConnectAuthFailureReason.MissingAuthTime
                    or OpenIdConnectAuthFailureReason.StaleAuthTime
                    or OpenIdConnectAuthFailureReason.MissingAcr
                    or OpenIdConnectAuthFailureReason.AcrNotAccepted
                    or OpenIdConnectAuthFailureReason.MissingAmr
                    or OpenIdConnectAuthFailureReason.AmrNotAccepted => ExternalAuthFailureReason.Rejected,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "The refusal reason has no category."),
            };
        }
    }
}
