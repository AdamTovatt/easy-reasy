using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Internal orchestrator behind the sign-in endpoints: begins a sign-in with the rules the consumer's
    /// <see cref="IMicrosoftAuthHandler"/> decides, and completes one by delegating the verified identity to that
    /// handler, surfacing the outcome as an <see cref="ExternalAuthResult"/> for audit logging.
    /// </summary>
    internal sealed class MicrosoftSignInService
    {
        /// <summary>
        /// The provider name recorded on every <see cref="ExternalAuthResult"/> this service produces.
        /// </summary>
        internal const string ProviderName = "microsoft";

        /// <summary>
        /// The caller binding every sign-in begun here carries, and the only one the sign-in endpoint accepts. A
        /// state an application began for its own flow (linking an account, a re-check at signing) carries the
        /// application's binding instead, so posting it to the sign-in endpoint cannot sign anyone in. Not public:
        /// an application has no reason to begin a sign-in flow by hand.
        /// </summary>
        internal const string SignInCallerBinding = "easyreasy.auth.microsoft:sign-in";

        /// <summary>
        /// The longest login hint the begin endpoint passes on; a longer one is ignored. Longer than any sign-in name
        /// (an email address is at most 254 characters), so it only stops an anonymous caller from having an
        /// arbitrarily large authorize URL built and returned.
        /// </summary>
        internal const int MaxLoginHintLength = 256;

        private readonly IMicrosoftAuthService _authService;
        private readonly IMicrosoftAuthHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftSignInService"/> class.
        /// </summary>
        /// <param name="authService">The Begin/Complete service.</param>
        /// <param name="handler">The consumer-provided handler for the sign-in's rules and the verified user.</param>
        public MicrosoftSignInService(IMicrosoftAuthService authService, IMicrosoftAuthHandler handler)
        {
            _authService = authService;
            _handler = handler;
        }

        /// <summary>
        /// Begins a sign-in with the handler's settings and the browser's login hint.
        /// </summary>
        /// <param name="loginHint">The login hint from the browser, or null. A blank one, or one longer than
        /// <see cref="MaxLoginHintLength"/>, is not sent.</param>
        /// <param name="httpContext">The begin request, which the binding cookie is set on.</param>
        /// <returns>Where to send the browser.</returns>
        /// <exception cref="ArgumentException">The handler's settings are refused by
        /// <see cref="IMicrosoftAuthService.Begin"/>.</exception>
        public async Task<MicrosoftSignInBeginResponse> BeginAsync(string? loginHint, HttpContext httpContext)
        {
            MicrosoftSignInSettings settings = await _handler.GetSignInSettingsAsync(httpContext);

            MicrosoftAuthBeginResult begun = _authService.Begin(new MicrosoftAuthBeginRequest
            {
                RedirectUri = settings.RedirectUri,
                CallerBinding = SignInCallerBinding,
                TenantId = settings.TenantId,
                Prompt = settings.Prompt,
                LoginHint = string.IsNullOrWhiteSpace(loginHint) || loginHint.Length > MaxLoginHintLength ? null : loginHint,
                RequiredAuthenticationContextId = settings.RequiredAuthenticationContextId,
                MaxAuthenticationAge = settings.MaxAuthenticationAge,
            }, httpContext);

            return new MicrosoftSignInBeginResponse(begun.AuthorizationUrl);
        }

        /// <summary>
        /// Completes a sign-in and delegates the verified identity to the handler.
        /// </summary>
        /// <param name="code">The authorization code from the redirect.</param>
        /// <param name="state">The <c>state</c> from the redirect.</param>
        /// <param name="jwtTokenService">The JWT token service for creating access tokens.</param>
        /// <param name="refreshTokenService">The refresh token service, or null if not configured.</param>
        /// <param name="httpContext">The sign-in request, whose binding cookie is checked and cleared.</param>
        /// <returns>
        /// Success with the issued <see cref="AuthResponse"/>, or a failure whose
        /// <see cref="ExternalAuthResult.FailureDetail"/> names the <see cref="MicrosoftAuthFailureReason"/>. A
        /// handler that returns null gives <see cref="ExternalAuthFailureReason.Rejected"/> with no detail.
        /// </returns>
        /// <exception cref="HttpRequestException">Microsoft's token endpoint could not be reached or failed.</exception>
        /// <exception cref="InvalidOperationException">Microsoft's signing keys could not be retrieved.</exception>
        public async Task<ExternalAuthResult> AuthenticateAsync(
            string code,
            string state,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext httpContext)
        {
            MicrosoftAuthCompletionResult completion = await _authService.CompleteAsync(code, state, httpContext, httpContext.RequestAborted);

            if (!completion.Success)
            {
                return Refuse(completion.FailureReason.Value, completion.AttemptedSubject);
            }

            if (!string.Equals(completion.CallerBinding, SignInCallerBinding, StringComparison.Ordinal))
            {
                return Refuse(MicrosoftAuthFailureReason.CallerBindingMismatch, completion.AttemptedSubject);
            }

            AuthResponse? authResponse = await _handler.HandleMicrosoftUserAsync(completion.UserInfo, jwtTokenService, refreshTokenService, httpContext);

            if (authResponse == null)
            {
                return ExternalAuthResult.Failed(ProviderName, ExternalAuthFailureReason.Rejected, completion.AttemptedSubject);
            }

            return ExternalAuthResult.Succeeded(ProviderName, authResponse, completion.AttemptedSubject);
        }

        private static ExternalAuthResult Refuse(MicrosoftAuthFailureReason reason, string? attemptedSubject)
        {
            return ExternalAuthResult.Failed(ProviderName, GetCategory(reason), attemptedSubject, reason.ToString());
        }

        /// <summary>
        /// The provider-neutral category of a refusal: a failure of the state, the binding cookie, the code or the
        /// token is <see cref="ExternalAuthFailureReason.InvalidToken"/>; a genuine identity refused by the flow's
        /// rules is <see cref="ExternalAuthFailureReason.Rejected"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A reason with no category, so a new member cannot ship
        /// unclassified.</exception>
        internal static ExternalAuthFailureReason GetCategory(MicrosoftAuthFailureReason reason)
        {
            return reason switch
            {
                MicrosoftAuthFailureReason.InvalidState
                    or MicrosoftAuthFailureReason.ExpiredState
                    or MicrosoftAuthFailureReason.BindingCookieMissing
                    or MicrosoftAuthFailureReason.BindingCookieMismatch
                    or MicrosoftAuthFailureReason.CodeRedemptionFailed
                    or MicrosoftAuthFailureReason.InvalidToken
                    or MicrosoftAuthFailureReason.InvalidSignature
                    or MicrosoftAuthFailureReason.InvalidAudience
                    or MicrosoftAuthFailureReason.InvalidIssuer
                    or MicrosoftAuthFailureReason.TokenExpired
                    or MicrosoftAuthFailureReason.NonceMismatch
                    or MicrosoftAuthFailureReason.CallerBindingMismatch => ExternalAuthFailureReason.InvalidToken,
                MicrosoftAuthFailureReason.PersonalAccount
                    or MicrosoftAuthFailureReason.TenantMismatch
                    or MicrosoftAuthFailureReason.TenantNotAllowed
                    or MicrosoftAuthFailureReason.MissingAuthenticationContext
                    or MicrosoftAuthFailureReason.MissingAuthTime
                    or MicrosoftAuthFailureReason.StaleAuthTime => ExternalAuthFailureReason.Rejected,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "The refusal reason has no category."),
            };
        }
    }
}
