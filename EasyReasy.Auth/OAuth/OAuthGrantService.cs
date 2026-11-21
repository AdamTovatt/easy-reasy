namespace EasyReasy.Auth
{
    /// <summary>
    /// The library's <see cref="IOAuthGrantService"/>, over the application's store.
    /// </summary>
    internal sealed class OAuthGrantService : IOAuthGrantService
    {
        private readonly IOAuthServerStore _store;

        /// <summary>
        /// Initializes the service.
        /// </summary>
        /// <param name="store">The application's store.</param>
        public OAuthGrantService(IOAuthServerStore store)
        {
            _store = store;
        }

        /// <inheritdoc />
        public async Task RevokeGrantAsync(string grantId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(grantId);

            // Codes first, as their own write: a redemption under way checks its code again after storing its refresh
            // token, so either it sees the code invalidated, or its token was stored before the sweep below and is caught.
            await _store.InvalidateGrantAuthorizationCodesAsync(grantId, cancellationToken);
            await _store.InvalidateGrantRefreshTokensAsync(grantId, cancellationToken);
        }
    }
}
