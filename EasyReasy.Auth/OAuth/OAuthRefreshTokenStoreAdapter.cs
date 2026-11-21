namespace EasyReasy.Auth
{
    /// <summary>
    /// Presents one grant's refresh tokens for one client in <see cref="IOAuthServerStore"/> as an
    /// <see cref="IRefreshTokenStore"/>, so the token endpoint runs <see cref="RefreshTokenService"/>'s rotation and reuse
    /// detection unchanged. One adapter serves one token request.
    /// </summary>
    /// <remarks>
    /// The token endpoint knows the client and grant before it builds the adapter: from the code for a redemption, from
    /// the presented refresh token's row for a refresh. A row of another client or grant reads as not found here, so it
    /// is neither rotated nor taken as reuse.
    /// </remarks>
    internal sealed class OAuthRefreshTokenStoreAdapter : IRefreshTokenStore
    {
        private readonly IOAuthServerStore _store;
        private readonly string _clientId;
        private readonly string _grantId;

        /// <summary>
        /// Initializes the adapter.
        /// </summary>
        /// <param name="store">The application's store.</param>
        /// <param name="clientId">The authenticated client.</param>
        /// <param name="grantId">The grant every row read and stored belongs to.</param>
        public OAuthRefreshTokenStoreAdapter(IOAuthServerStore store, string clientId, string grantId)
        {
            _store = store;
            _clientId = clientId;
            _grantId = grantId;
        }

        /// <inheritdoc />
        public Task StoreAsync(StoredRefreshToken refreshToken, CancellationToken cancellationToken = default)
        {
            OAuthRefreshToken row = new OAuthRefreshToken
            {
                TokenHash = refreshToken.TokenHash,
                FamilyId = refreshToken.FamilyId,
                GrantId = _grantId,
                ClientId = _clientId,
                Subject = refreshToken.Subject,
                AuthType = refreshToken.AuthType,
                SerializedClaims = refreshToken.SerializedClaims,
                SerializedRoles = refreshToken.SerializedRoles,
                CreatedAt = refreshToken.CreatedAt,
                ExpiresAt = refreshToken.ExpiresAt,
                ConsumedAt = refreshToken.ConsumedAt,
                IsInvalidated = refreshToken.IsInvalidated,
            };
            return _store.StoreRefreshTokenAsync(row, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<StoredRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            OAuthRefreshToken? row = await _store.GetRefreshTokenAsync(tokenHash, cancellationToken);
            if (row == null
                || !string.Equals(row.ClientId, _clientId, StringComparison.Ordinal)
                || !string.Equals(row.GrantId, _grantId, StringComparison.Ordinal))
            {
                return null;
            }

            return new StoredRefreshToken
            {
                TokenHash = row.TokenHash,
                Subject = row.Subject,
                AuthType = row.AuthType,
                FamilyId = row.FamilyId,
                CreatedAt = row.CreatedAt,
                ExpiresAt = row.ExpiresAt,
                ConsumedAt = row.ConsumedAt,
                IsInvalidated = row.IsInvalidated,
                SerializedClaims = row.SerializedClaims,
                SerializedRoles = row.SerializedRoles,
            };
        }

        /// <inheritdoc />
        public Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default)
        {
            return _store.ConsumeRefreshTokenAsync(tokenHash, consumedAt, cancellationToken);
        }

        /// <inheritdoc />
        public Task InvalidateFamilyAsync(string familyId, CancellationToken cancellationToken = default)
        {
            return _store.InvalidateRefreshTokenFamilyAsync(familyId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<int> InvalidateAllFamiliesForUserAsync(string subject, CancellationToken cancellationToken = default)
        {
            // Review skip (S2): left unsupported rather than given a meaning. The service is built per request by the token
            // endpoint with ConcurrentSessionPolicy.AllowMultiple and only rotates, so nothing reaches this; a client's
            // connections end by grant (IOAuthGrantService) or family (revocation), never by subject.
            throw new NotSupportedException("A client's refresh tokens are revoked by grant or family, not by subject.");
        }
    }
}
