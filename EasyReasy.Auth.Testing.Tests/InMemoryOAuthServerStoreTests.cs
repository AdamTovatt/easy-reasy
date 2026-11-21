namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// <see cref="InMemoryOAuthServerStore"/> keeps the store contract: each compare-and-set succeeds for the first caller
    /// only, invalidation reaches exactly the rows it names, and what it returns is a copy.
    /// </summary>
    [TestClass]
    public class InMemoryOAuthServerStoreTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public async Task CompleteAuthorizationRequestAsync_SecondCall_IsFalse()
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreAuthorizationRequestAsync(Request("request-1"));

            Assert.IsTrue(await store.CompleteAuthorizationRequestAsync("request-1", Now));
            Assert.IsFalse(await store.CompleteAuthorizationRequestAsync("request-1", Now));
            Assert.IsFalse(await store.CompleteAuthorizationRequestAsync("unknown", Now));
        }

        [TestMethod]
        public async Task RedeemAuthorizationCodeAsync_ConcurrentCalls_OnlyOneSucceeds()
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreAuthorizationCodeAsync(Code("code-1", "grant-1"));

            bool[] results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => store.RedeemAuthorizationCodeAsync("code-1", Now))));

            Assert.AreEqual(1, results.Count(result => result));
            Assert.AreEqual(Now, (await store.GetAuthorizationCodeAsync("code-1"))!.RedeemedAt);
        }

        [TestMethod]
        public async Task ConsumeRefreshTokenAsync_SecondCall_IsFalse()
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreRefreshTokenAsync(Token("token-1", "family-1", "grant-1"));

            Assert.IsTrue(await store.ConsumeRefreshTokenAsync("token-1", Now));
            Assert.IsFalse(await store.ConsumeRefreshTokenAsync("token-1", Now));
        }

        [TestMethod]
        public async Task InvalidateGrant_ReachesOnlyThatGrantsCodesAndTokens()
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreAuthorizationCodeAsync(Code("code-1", "grant-1"));
            await store.StoreAuthorizationCodeAsync(Code("code-2", "grant-2"));
            await store.RedeemAuthorizationCodeAsync("code-1", Now);
            await store.StoreRefreshTokenAsync(Token("token-1", "family-1", "grant-1"));
            await store.StoreRefreshTokenAsync(Token("token-2", "family-2", "grant-2"));

            await store.InvalidateGrantAuthorizationCodesAsync("grant-1");
            await store.InvalidateGrantRefreshTokensAsync("grant-1");

            Assert.IsTrue((await store.GetAuthorizationCodeAsync("code-1"))!.IsInvalidated);
            Assert.IsFalse((await store.GetAuthorizationCodeAsync("code-2"))!.IsInvalidated);
            Assert.IsTrue((await store.GetRefreshTokenAsync("token-1"))!.IsInvalidated);
            Assert.IsFalse((await store.GetRefreshTokenAsync("token-2"))!.IsInvalidated);
        }

        [TestMethod]
        public async Task InvalidateRefreshTokenFamilyAsync_ReachesOnlyThatFamily()
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreRefreshTokenAsync(Token("token-1", "family-1", "grant-1"));
            await store.StoreRefreshTokenAsync(Token("token-2", "family-2", "grant-1"));

            await store.InvalidateRefreshTokenFamilyAsync("family-1");

            Assert.IsTrue((await store.GetRefreshTokenAsync("token-1"))!.IsInvalidated);
            Assert.IsFalse((await store.GetRefreshTokenAsync("token-2"))!.IsInvalidated);
        }

        [TestMethod]
        public async Task GetAuthorizationCodeAsync_ReturnsACopy()
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreAuthorizationCodeAsync(Code("code-1", "grant-1"));
            OAuthAuthorizationCode read = (await store.GetAuthorizationCodeAsync("code-1"))!;

            await store.InvalidateAuthorizationCodeAsync("code-1");
            read.RedeemedAt = Now;

            Assert.IsFalse(read.IsInvalidated);
            Assert.IsNull((await store.GetAuthorizationCodeAsync("code-1"))!.RedeemedAt);
        }

        private static OAuthAuthorizationRequest Request(string hash)
        {
            return new OAuthAuthorizationRequest
            {
                RequestHash = hash,
                ClientId = "client-1",
                ClientRegistrationKind = OAuthClientRegistrationKind.Registered,
                RedirectUri = "https://client.example/callback",
                CodeChallenge = "challenge",
                ExpiresAt = Now.AddMinutes(10),
            };
        }

        private static OAuthAuthorizationCode Code(string hash, string grantId)
        {
            return new OAuthAuthorizationCode
            {
                CodeHash = hash,
                ClientId = "client-1",
                RedirectUri = "https://client.example/callback",
                CodeChallenge = "challenge",
                GrantId = grantId,
                Subject = "person-1",
                AuthType = "user",
                FamilyId = "family-of-" + hash,
                ExpiresAt = Now.AddMinutes(1),
            };
        }

        private static OAuthRefreshToken Token(string hash, string familyId, string grantId)
        {
            return new OAuthRefreshToken
            {
                TokenHash = hash,
                FamilyId = familyId,
                GrantId = grantId,
                ClientId = "client-1",
                Subject = "person-1",
                AuthType = "user",
                CreatedAt = Now,
                ExpiresAt = Now.AddDays(30),
            };
        }
    }
}
