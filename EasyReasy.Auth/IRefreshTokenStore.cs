namespace EasyReasy.Auth
{
    /// <summary>
    /// Interface for consumer-implemented refresh token storage.
    /// The library stays database-agnostic — consumers implement this interface
    /// to persist refresh tokens in their preferred storage mechanism.
    /// </summary>
    public interface IRefreshTokenStore
    {
        /// <summary>
        /// Stores a new refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token to store.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task StoreAsync(StoredRefreshToken refreshToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a stored refresh token by its hash.
        /// </summary>
        /// <param name="tokenHash">The SHA-256 hash of the raw refresh token.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The stored refresh token, or null if not found.</returns>
        Task<StoredRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically marks a refresh token as consumed (used to obtain a new token pair).
        /// The implementation must ensure that only the first caller succeeds when concurrent
        /// requests attempt to consume the same token (e.g., using <c>UPDATE ... WHERE consumed_at IS NULL</c>
        /// and checking affected rows in SQL, or equivalent atomic operations in other stores).
        /// </summary>
        /// <param name="tokenHash">The SHA-256 hash of the consumed refresh token.</param>
        /// <param name="consumedAt">The UTC time when the token was consumed.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True if the token was successfully marked as consumed; false if it was already consumed by another request.</returns>
        Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates all refresh tokens in a token family.
        /// Called when token theft is detected (a consumed token is reused) or when a user logs out.
        /// </summary>
        /// <param name="familyId">The family identifier of the token chain to invalidate.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task InvalidateFamilyAsync(string familyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates every non-invalidated refresh token family for the specified subject.
        /// Used for bulk session revocation scenarios such as password change, role demotion,
        /// or admin-forced logout.
        /// </summary>
        /// <param name="subject">The subject (user identifier) whose sessions should be revoked.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// The number of token families that were invalidated by the operation. Implementations should
        /// count distinct families (not individual tokens) so that the value reflects the number of
        /// real sessions terminated — this flows into audit logs via <see cref="SessionRevocationResult"/>.
        /// </returns>
        Task<int> InvalidateAllFamiliesForUserAsync(string subject, CancellationToken cancellationToken = default);
    }
}
