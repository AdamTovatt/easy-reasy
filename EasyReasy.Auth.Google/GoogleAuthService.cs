using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Internal orchestrator that validates a Google ID token and delegates to the
    /// consumer-provided <see cref="IGoogleAuthHandler"/> to produce an <see cref="AuthResponse"/>,
    /// surfacing the outcome as an <see cref="ExternalAuthResult"/> for audit logging.
    /// </summary>
    internal class GoogleAuthService
    {
        /// <summary>
        /// The provider name recorded on every <see cref="ExternalAuthResult"/> this service produces.
        /// </summary>
        private const string ProviderName = "google";

        private readonly IGoogleIdTokenValidator _tokenValidator;
        private readonly IGoogleAuthHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleAuthService"/> class.
        /// </summary>
        /// <param name="tokenValidator">The token validator for verifying Google ID tokens.</param>
        /// <param name="handler">The consumer-provided handler for processing authenticated Google users.</param>
        public GoogleAuthService(IGoogleIdTokenValidator tokenValidator, IGoogleAuthHandler handler)
        {
            _tokenValidator = tokenValidator;
            _handler = handler;
        }

        /// <summary>
        /// Validates the Google ID token and delegates to the handler to produce an auth response.
        /// </summary>
        /// <param name="idToken">The Google ID token to validate.</param>
        /// <param name="jwtTokenService">The JWT token service for creating access tokens.</param>
        /// <param name="refreshTokenService">The refresh token service, or null if not configured.</param>
        /// <param name="httpContext">The HTTP context, or null.</param>
        /// <returns>
        /// An <see cref="ExternalAuthResult"/> describing the outcome: success with the issued
        /// <see cref="AuthResponse"/>, <see cref="ExternalAuthFailureReason.InvalidToken"/> when the Google
        /// token fails validation, or <see cref="ExternalAuthFailureReason.Rejected"/> when the handler
        /// declines an otherwise-valid identity.
        /// </returns>
        public async Task<ExternalAuthResult> AuthenticateAsync(
            string idToken,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext? httpContext)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return ExternalAuthResult.Failed(ProviderName, ExternalAuthFailureReason.InvalidToken);
            }

            GoogleUserInfo userInfo;

            try
            {
                userInfo = await _tokenValidator.ValidateAsync(idToken);
            }
            catch (GoogleTokenValidationException)
            {
                return ExternalAuthResult.Failed(ProviderName, ExternalAuthFailureReason.InvalidToken);
            }

            AuthResponse? authResponse = await _handler.HandleGoogleUserAsync(userInfo, jwtTokenService, refreshTokenService, httpContext);

            if (authResponse == null)
            {
                return ExternalAuthResult.Failed(ProviderName, ExternalAuthFailureReason.Rejected, userInfo.Subject);
            }

            return ExternalAuthResult.Succeeded(ProviderName, authResponse, userInfo.Subject);
        }
    }
}
