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
        Task StoreAsync(StoredRefreshToken refreshToken);

        /// <summary>
        /// Retrieves a stored refresh token by its hash.
        /// </summary>
        /// <param name="tokenHash">The SHA-256 hash of the raw refresh token.</param>
        /// <returns>The stored refresh token, or null if not found.</returns>
        Task<StoredRefreshToken?> GetByTokenHashAsync(string tokenHash);

        /// <summary>
        /// Atomically marks a refresh token as consumed (used to obtain a new token pair).
        /// The implementation must ensure that only the first caller succeeds when concurrent
        /// requests attempt to consume the same token (e.g., using <c>UPDATE ... WHERE consumed_at IS NULL</c>
        /// and checking affected rows in SQL, or equivalent atomic operations in other stores).
        /// </summary>
        /// <param name="tokenHash">The SHA-256 hash of the consumed refresh token.</param>
        /// <param name="consumedAt">The UTC time when the token was consumed.</param>
        /// <returns>True if the token was successfully marked as consumed; false if it was already consumed by another request.</returns>
        Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt);

        /// <summary>
        /// Invalidates all refresh tokens in a token family.
        /// Called when token theft is detected (a consumed token is reused).
        /// </summary>
        /// <param name="familyId">The family identifier of the token chain to invalidate.</param>
        Task InvalidateFamilyAsync(string familyId);
    }
}
