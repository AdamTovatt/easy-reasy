namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A refresh that a logout or a replay lands in the middle of, after the presented token is consumed and before the
    /// next one is stored. The next one must not outlive it.
    /// </summary>
    [TestClass]
    public class RefreshTokenServiceRaceTests
    {
        private const string TestSecret = "super_secret_key_12345_12345_12345";
        private const string TestIssuer = "test-issuer";

        [TestMethod]
        public async Task RefreshAsync_LogoutDuringTheRefresh_FailsAndLeavesNoLiveToken()
        {
            InterceptingRefreshTokenStore store = new InterceptingRefreshTokenStore();
            RefreshTokenService service = new RefreshTokenService(store);
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            store.BeforeNextStore(() => service.LogoutAsync(rawToken));

            RefreshResult result = await service.RefreshAsync(rawToken, new JwtTokenService(TestSecret, TestIssuer));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RefreshFailureReason.TokenInvalidated, result.FailureReason);
            Assert.AreEqual(2, store.Inner.Tokens.Count);
            Assert.IsTrue(store.Inner.Tokens.Values.All(token => token.IsInvalidated));
        }

        [TestMethod]
        public async Task RefreshAsync_ReplayedDuringTheRefresh_FailsBothAndLeavesNoLiveToken()
        {
            InterceptingRefreshTokenStore store = new InterceptingRefreshTokenStore();
            RefreshTokenService service = new RefreshTokenService(store);
            IJwtTokenService jwtTokenService = new JwtTokenService(TestSecret, TestIssuer);
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            RefreshResult? replay = null;
            store.BeforeNextStore(async () => replay = await service.RefreshAsync(rawToken, jwtTokenService));

            RefreshResult result = await service.RefreshAsync(rawToken, jwtTokenService);

            Assert.AreEqual(RefreshFailureReason.TheftDetected, replay?.FailureReason);
            Assert.AreEqual(RefreshFailureReason.TokenInvalidated, result.FailureReason);
            Assert.AreEqual(2, store.Inner.Tokens.Count);
            Assert.IsTrue(store.Inner.Tokens.Values.All(token => token.IsInvalidated));
        }

        /// <summary>
        /// Passes every call to a <see cref="FakeRefreshTokenStore"/>, and runs something once just before the next token
        /// is stored.
        /// </summary>
        private sealed class InterceptingRefreshTokenStore : IRefreshTokenStore
        {
            private Func<Task>? _beforeNextStore;

            public FakeRefreshTokenStore Inner { get; } = new FakeRefreshTokenStore();

            public void BeforeNextStore(Func<Task> action)
            {
                _beforeNextStore = action;
            }

            public async Task StoreAsync(StoredRefreshToken refreshToken, CancellationToken cancellationToken = default)
            {
                Func<Task>? action = Interlocked.Exchange(ref _beforeNextStore, null);
                if (action != null)
                {
                    await action();
                }

                await Inner.StoreAsync(refreshToken, cancellationToken);
            }

            public Task<StoredRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => Inner.GetByTokenHashAsync(tokenHash, cancellationToken);

            public Task<bool> MarkAsConsumedAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default) => Inner.MarkAsConsumedAsync(tokenHash, consumedAt, cancellationToken);

            public Task InvalidateFamilyAsync(string familyId, CancellationToken cancellationToken = default) => Inner.InvalidateFamilyAsync(familyId, cancellationToken);

            public Task<int> InvalidateAllFamiliesForUserAsync(string subject, CancellationToken cancellationToken = default) => Inner.InvalidateAllFamiliesForUserAsync(subject, cancellationToken);
        }
    }
}
