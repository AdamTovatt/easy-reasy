namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// <see cref="InMemoryOAuthServerStore"/> keeps the store contract: each compare-and-set succeeds for the first caller
    /// only, invalidation reaches exactly the rows it names, what it returns is a copy, and the sweep's delete removes
    /// exactly what the contract names.
    /// </summary>
    [TestClass]
    public class InMemoryOAuthServerStoreTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Cutoff = Now.AddDays(-30);

        /// <summary>
        /// What a client registered before the cutoff holds when the sweep runs.
        /// </summary>
        public enum ClientHolds
        {
            Nothing,
            Request,
            CompletedRequest,
            Code,
            RedeemedCode,
            Token,
            ConsumedToken,
            InvalidatedToken,
            ExpiredToken,
        }

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

        [TestMethod]
        public async Task DeleteExpiredAsync_RowsAtOrPastTheirExpiry_AreDeletedWhateverTheirState()
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreAuthorizationRequestAsync(Request("request-expired", expiresAt: Now));
            await store.StoreAuthorizationRequestAsync(Request("request-live", expiresAt: Now.AddSeconds(1)));
            await store.StoreAuthorizationCodeAsync(Code("code-expired", expiresAt: Now));
            await store.StoreAuthorizationCodeAsync(Code("code-live", expiresAt: Now.AddSeconds(1)));
            await store.StoreRefreshTokenAsync(Token("token-expired-consumed", expiresAt: Now));
            await store.StoreRefreshTokenAsync(Token("token-live", expiresAt: Now.AddSeconds(1)));
            await store.ConsumeRefreshTokenAsync("token-expired-consumed", Now.AddMinutes(-1));

            await store.DeleteExpiredAsync(Now, Cutoff);

            CollectionAssert.AreEqual(new[] { "request-live" }, store.AuthorizationRequests.Select(request => request.RequestHash).ToArray());
            CollectionAssert.AreEqual(new[] { "code-live" }, store.AuthorizationCodes.Select(code => code.CodeHash).ToArray());
            CollectionAssert.AreEqual(new[] { "token-live" }, store.RefreshTokens.Select(token => token.TokenHash).ToArray());
        }

        [TestMethod]
        [DataRow(ClientHolds.Nothing, true, DisplayName = "nothing: deleted")]
        [DataRow(ClientHolds.Request, false, DisplayName = "an authorization under way: kept")]
        [DataRow(ClientHolds.CompletedRequest, false, DisplayName = "an unexpired completed request: kept")]
        [DataRow(ClientHolds.Code, false, DisplayName = "a live code: kept")]
        [DataRow(ClientHolds.RedeemedCode, false, DisplayName = "an unexpired redeemed code: kept")]
        [DataRow(ClientHolds.Token, false, DisplayName = "a live refresh token: kept")]
        [DataRow(ClientHolds.ConsumedToken, false, DisplayName = "an unexpired consumed refresh token: kept")]
        [DataRow(ClientHolds.InvalidatedToken, false, DisplayName = "an unexpired invalidated refresh token: kept")]
        [DataRow(ClientHolds.ExpiredToken, true, DisplayName = "only an expired refresh token: deleted")]
        public async Task DeleteExpiredAsync_ClientRegisteredBeforeTheCutoff_IsDeletedOnlyWhenNoUnexpiredRowNamesIt(ClientHolds holds, bool deletedExpected)
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreClientAsync(Client("client-old", Cutoff.AddDays(-1)));
            switch (holds)
            {
                case ClientHolds.Request:
                case ClientHolds.CompletedRequest:
                    await store.StoreAuthorizationRequestAsync(Request("request-1", "client-old", Now.AddMinutes(5)));
                    if (holds == ClientHolds.CompletedRequest)
                    {
                        await store.CompleteAuthorizationRequestAsync("request-1", Now);
                    }

                    break;
                case ClientHolds.Code:
                case ClientHolds.RedeemedCode:
                    await store.StoreAuthorizationCodeAsync(Code("code-1", clientId: "client-old", expiresAt: Now.AddMinutes(1)));
                    if (holds == ClientHolds.RedeemedCode)
                    {
                        await store.RedeemAuthorizationCodeAsync("code-1", Now);
                    }

                    break;
                case ClientHolds.Token:
                case ClientHolds.ConsumedToken:
                case ClientHolds.InvalidatedToken:
                    await store.StoreRefreshTokenAsync(Token("token-1", clientId: "client-old", expiresAt: Now.AddDays(1)));
                    if (holds == ClientHolds.ConsumedToken)
                    {
                        await store.ConsumeRefreshTokenAsync("token-1", Now);
                    }
                    else if (holds == ClientHolds.InvalidatedToken)
                    {
                        await store.InvalidateRefreshTokenFamilyAsync("family-1");
                    }

                    break;
                case ClientHolds.ExpiredToken:
                    await store.StoreRefreshTokenAsync(Token("token-1", clientId: "client-old", expiresAt: Now.AddDays(-1)));
                    break;
            }

            await store.DeleteExpiredAsync(Now, Cutoff);

            Assert.AreEqual(deletedExpected ? 0 : 1, store.Clients.Count);
        }

        [TestMethod]
        [DataRow(0, DisplayName = "registered at the cutoff")]
        [DataRow(1, DisplayName = "registered a minute after it")]
        public async Task DeleteExpiredAsync_ClientRegisteredAtOrAfterTheCutoff_IsKeptWithNothingLive(int minutesAfterCutoff)
        {
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            await store.StoreClientAsync(Client("client-new", Cutoff.AddMinutes(minutesAfterCutoff)));

            await store.DeleteExpiredAsync(Now, Cutoff);

            Assert.AreEqual("client-new", store.Clients.Single().ClientId);
        }

        private static OAuthClient Client(string clientId, DateTime createdAt)
        {
            return new OAuthClient
            {
                ClientId = clientId,
                AuthenticationMethod = OAuthClientAuthenticationMethod.None,
                RedirectUris = ["https://client.example/callback"],
                CreatedAt = createdAt,
            };
        }

        private static OAuthAuthorizationRequest Request(string hash, string clientId = "client-1", DateTime? expiresAt = null)
        {
            return new OAuthAuthorizationRequest
            {
                RequestHash = hash,
                ClientId = clientId,
                RedirectUri = "https://client.example/callback",
                CodeChallenge = "challenge",
                ExpiresAt = expiresAt ?? Now.AddMinutes(10),
            };
        }

        private static OAuthAuthorizationCode Code(string hash, string grantId = "grant-1", string clientId = "client-1", DateTime? expiresAt = null)
        {
            return new OAuthAuthorizationCode
            {
                CodeHash = hash,
                ClientId = clientId,
                RedirectUri = "https://client.example/callback",
                CodeChallenge = "challenge",
                GrantId = grantId,
                Subject = "person-1",
                AuthType = "user",
                FamilyId = "family-of-" + hash,
                ExpiresAt = expiresAt ?? Now.AddMinutes(1),
            };
        }

        private static OAuthRefreshToken Token(string hash, string familyId = "family-1", string grantId = "grant-1", string clientId = "client-1", DateTime? expiresAt = null)
        {
            DateTime expires = expiresAt ?? Now.AddDays(30);
            return new OAuthRefreshToken
            {
                TokenHash = hash,
                FamilyId = familyId,
                GrantId = grantId,
                ClientId = clientId,
                Subject = "person-1",
                AuthType = "user",
                CreatedAt = expires.AddDays(-30),
                ExpiresAt = expires,
            };
        }
    }
}
