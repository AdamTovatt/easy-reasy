using Microsoft.AspNetCore.Http;

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
        /// <remarks>
        /// When an <see cref="IRefreshClaimsResolver"/> is registered, the implementation
        /// invokes it before the atomic consume to either re-evaluate the claims and roles
        /// that ride onto the new tokens (replacing what was stored at login time) or deny
        /// the refresh outright with <see cref="RefreshFailureReason.DeniedByResolver"/>.
        /// </remarks>
        /// <param name="refreshToken">The raw refresh token from the client.</param>
        /// <param name="jwtTokenService">The JWT token service used to create the new access token.</param>
        /// <param name="httpContext">
        /// The HTTP context of the triggering request, when called from an HTTP endpoint.
        /// Pass <c>null</c> for programmatic refreshes (e.g. from a background service). Used to
        /// propagate context to both <see cref="IAuthAuditLogger.OnRefreshAsync"/> and
        /// <see cref="IRefreshClaimsResolver.ResolveAsync"/>.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A <see cref="RefreshResult"/> indicating success or failure.</returns>
        Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService, HttpContext? httpContext = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes the refresh token family that the supplied token belongs to.
        /// The operation is idempotent — unknown, already-consumed, or already-invalidated
        /// tokens complete silently without throwing, so callers do not leak which tokens exist.
        /// Null or empty tokens are accepted and treated as a no-op for the same reason.
        /// </summary>
        /// <param name="refreshToken">
        /// The raw refresh token to log out. May be <c>null</c> or empty — treated as a no-op in either case
        /// so the HTTP endpoint can respond 204 regardless of input.
        /// </param>
        /// <param name="httpContext">
        /// The HTTP context of the triggering request, when called from an HTTP endpoint.
        /// Pass <c>null</c> for programmatic logouts (e.g. from a background service). Only used to
        /// propagate context to <see cref="IAuthAuditLogger.OnLogoutAsync"/>.
        /// </param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="LogoutResult"/> describing whether the token matched a stored family and,
        /// if so, the subject and family identifier that were invalidated. The built-in endpoint
        /// does not return this to the client (it always responds 204), but it is surfaced here
        /// so consumers can drive audit logging.
        /// </returns>
        Task<LogoutResult> LogoutAsync(string? refreshToken, HttpContext? httpContext = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates every refresh token family for the specified subject.
        /// Intended for password change, role demotion, and admin-forced logout flows.
        /// </summary>
        /// <param name="subject">The subject (user identifier) whose sessions should be revoked.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="SessionRevocationResult"/> containing the subject and the number of
        /// refresh token families that were invalidated. A count of zero means the subject had
        /// no active sessions at the time of the call.
        /// </returns>
        Task<SessionRevocationResult> InvalidateAllSessionsAsync(string subject, CancellationToken cancellationToken = default);
    }
}
