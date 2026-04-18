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
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The raw refresh token string to return to the client.</returns>
        Task<string> CreateRefreshTokenAsync(string subject, string authType, string? serializedClaims, string? serializedRoles, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a refresh token and issues a new access token and refresh token pair.
        /// Implements token rotation with theft detection — if a consumed token is reused,
        /// the entire token family is invalidated.
        /// </summary>
        /// <param name="refreshToken">The raw refresh token from the client.</param>
        /// <param name="jwtTokenService">The JWT token service used to create the new access token.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A <see cref="RefreshResult"/> indicating success or failure.</returns>
        Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes the refresh token family that the supplied token belongs to.
        /// The operation is idempotent — unknown, already-consumed, or already-invalidated
        /// tokens complete silently without throwing, so callers do not leak which tokens exist.
        /// </summary>
        /// <param name="refreshToken">The raw refresh token to log out.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates every refresh token family for the specified subject.
        /// Intended for password change, role demotion, and admin-forced logout flows.
        /// </summary>
        /// <param name="subject">The subject (user identifier) whose sessions should be revoked.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task InvalidateAllSessionsAsync(string subject, CancellationToken cancellationToken = default);
    }
}
