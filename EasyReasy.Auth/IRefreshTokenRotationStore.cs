namespace EasyReasy.Auth
{
    /// <summary>
    /// The refresh token operations that creating a family and rotating it need: store, look up, consume, and invalidate
    /// a family. <see cref="IRefreshTokenStore"/> extends it with the subject-wide revocation a first-party session needs.
    /// </summary>
    public interface IRefreshTokenRotationStore
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
        /// Invalidates all refresh tokens in a token family, consumed ones included.
        /// Called when token theft is detected (a consumed token is reused) or when a user logs out.
        /// </summary>
        /// <remarks>
        /// A refresh under way reads the token it consumed again after storing the next one, and gives the next one up
        /// if the consumed token is invalidated by then. So the consumed token must be marked too, and the write
        /// committed before this returns.
        /// </remarks>
        /// <param name="familyId">The family identifier of the token chain to invalidate.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task InvalidateFamilyAsync(string familyId, CancellationToken cancellationToken = default);
    }
}
