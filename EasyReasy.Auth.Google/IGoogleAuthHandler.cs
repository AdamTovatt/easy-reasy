using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Consumer-implemented interface for handling authenticated Google users.
    /// This is the integration point where the consuming application decides how to
    /// handle a verified Google identity (e.g., find or create the user, assign claims/roles,
    /// and issue a JWT).
    /// </summary>
    public interface IGoogleAuthHandler
    {
        /// <summary>
        /// Handles an authenticated Google user by looking up or creating the user
        /// and returning an <see cref="AuthResponse"/> with a JWT token.
        /// </summary>
        /// <param name="userInfo">The validated Google user information.</param>
        /// <param name="jwtTokenService">The JWT token service for creating access tokens.</param>
        /// <param name="refreshTokenService">The refresh token service, or null if refresh tokens are not configured.</param>
        /// <param name="httpContext">The HTTP context containing request information, or null.</param>
        /// <returns>An <see cref="AuthResponse"/> if the user is accepted, or null to reject the request.</returns>
        Task<AuthResponse?> HandleGoogleUserAsync(
            GoogleUserInfo userInfo,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext? httpContext = null);
    }
}
