namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The server's store in <see cref="HostedOAuthServer"/>: passes every call to an <see cref="InMemoryOAuthServerStore"/>,
    /// and lets a test run something just before the next refresh token is stored, to put another request in the middle
    /// of a redemption.
    /// </summary>
    internal sealed class InterceptingOAuthServerStore : IOAuthServerStore
    {
        private readonly InMemoryOAuthServerStore _inner;
        private Func<Task>? _beforeNextRefreshTokenStore;

        public InterceptingOAuthServerStore(InMemoryOAuthServerStore inner)
        {
            _inner = inner;
        }

        /// <summary>
        /// Runs <paramref name="action"/> once, when the next refresh token is about to be stored.
        /// </summary>
        public void BeforeNextRefreshTokenStore(Func<Task> action)
        {
            _beforeNextRefreshTokenStore = action;
        }

        public async Task StoreRefreshTokenAsync(OAuthRefreshToken refreshToken, CancellationToken cancellationToken = default)
        {
            Func<Task>? action = Interlocked.Exchange(ref _beforeNextRefreshTokenStore, null);
            if (action != null)
            {
                await action();
            }

            await _inner.StoreRefreshTokenAsync(refreshToken, cancellationToken);
        }

        public Task StoreClientAsync(OAuthClient client, CancellationToken cancellationToken = default) => _inner.StoreClientAsync(client, cancellationToken);

        public Task<OAuthClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default) => _inner.GetClientAsync(clientId, cancellationToken);

        public Task StoreAuthorizationRequestAsync(OAuthAuthorizationRequest request, CancellationToken cancellationToken = default) => _inner.StoreAuthorizationRequestAsync(request, cancellationToken);

        public Task<OAuthAuthorizationRequest?> GetAuthorizationRequestAsync(string requestHash, CancellationToken cancellationToken = default) => _inner.GetAuthorizationRequestAsync(requestHash, cancellationToken);

        public Task<bool> CompleteAuthorizationRequestAsync(string requestHash, DateTime completedAt, CancellationToken cancellationToken = default) => _inner.CompleteAuthorizationRequestAsync(requestHash, completedAt, cancellationToken);

        public Task StoreAuthorizationCodeAsync(OAuthAuthorizationCode code, CancellationToken cancellationToken = default) => _inner.StoreAuthorizationCodeAsync(code, cancellationToken);

        public Task<OAuthAuthorizationCode?> GetAuthorizationCodeAsync(string codeHash, CancellationToken cancellationToken = default) => _inner.GetAuthorizationCodeAsync(codeHash, cancellationToken);

        public Task<bool> RedeemAuthorizationCodeAsync(string codeHash, DateTime redeemedAt, CancellationToken cancellationToken = default) => _inner.RedeemAuthorizationCodeAsync(codeHash, redeemedAt, cancellationToken);

        public Task<OAuthRefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default) => _inner.GetRefreshTokenAsync(tokenHash, cancellationToken);

        public Task<bool> ConsumeRefreshTokenAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default) => _inner.ConsumeRefreshTokenAsync(tokenHash, consumedAt, cancellationToken);

        public Task InvalidateRefreshTokenFamilyAsync(string familyId, CancellationToken cancellationToken = default) => _inner.InvalidateRefreshTokenFamilyAsync(familyId, cancellationToken);

        public Task InvalidateAuthorizationCodeAsync(string codeHash, CancellationToken cancellationToken = default) => _inner.InvalidateAuthorizationCodeAsync(codeHash, cancellationToken);

        public Task InvalidateGrantAuthorizationCodesAsync(string grantId, CancellationToken cancellationToken = default) => _inner.InvalidateGrantAuthorizationCodesAsync(grantId, cancellationToken);

        public Task InvalidateGrantRefreshTokensAsync(string grantId, CancellationToken cancellationToken = default) => _inner.InvalidateGrantRefreshTokensAsync(grantId, cancellationToken);
    }
}
