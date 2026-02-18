namespace EasyReasy.Auth
{
    /// <summary>
    /// Service for creating and refreshing tokens using refresh token rotation.
    /// This is the library-provided service that coordinates token generation,
    /// storage, and rotation with theft detection.
    /// </summary>
    public interface IRefreshTokenService
    {
        /// <summary>
        /// Creates a new refresh token for the specified subject and stores it.
        /// </summary>
        /// <param name="subject">The subject (user identifier) the token is for.</param>
        /// <param name="authType">The authentication type (e.g., "apikey" or "user").</param>
        /// <param name="serializedClaims">JSON-serialized additional claims, or null if none.</param>
        /// <param name="serializedRoles">JSON-serialized roles, or null if none.</param>
        /// <returns>The raw refresh token string to return to the client.</returns>
        Task<string> CreateRefreshTokenAsync(string subject, string authType, string? serializedClaims, string? serializedRoles);

        /// <summary>
        /// Validates a refresh token and issues a new access token and refresh token pair.
        /// Implements token rotation with theft detection — if a consumed token is reused,
        /// the entire token family is invalidated.
        /// </summary>
        /// <param name="refreshToken">The raw refresh token from the client.</param>
        /// <param name="jwtTokenService">The JWT token service used to create the new access token.</param>
        /// <returns>A <see cref="RefreshResult"/> indicating success or failure.</returns>
        Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService);
    }
}
