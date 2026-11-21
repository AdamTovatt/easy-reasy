namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A redemption or refresh that another request lands in the middle of, just before its refresh token is stored: a
    /// second presentation of the code or refresh token, or a revocation. Nothing the first request issues may outlive it.
    /// </summary>
    [TestClass]
    public class OAuthRedemptionRaceTests
    {
        private const string RedirectUri = "https://client.example/callback";

        [TestMethod]
        public async Task RedeemCode_PresentedAgainDuringTheFirstRedemption_RefusesBothAndLeavesNoLiveToken()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync();
            OAuthTestFlowException? second = null;
            server.Interceptor.BeforeNextRefreshTokenStore(async () =>
                second = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RedeemCodeAsync(authorization, code)));

            OAuthTestFlowException first = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RedeemCodeAsync(authorization, code));

            Assert.AreEqual("invalid_grant", second?.Error);
            Assert.AreEqual("invalid_grant", first.Error);
            CollectionAssert.AreEqual(
                new OAuthTokenFailureReason?[] { OAuthTokenFailureReason.CodeReused, OAuthTokenFailureReason.CodeInvalidated },
                server.AuditLogger.Tokens.Select(result => result.FailureReason).ToArray());
            Assert.IsTrue(server.Store.RefreshTokens.Single().IsInvalidated);
        }

        [TestMethod]
        public async Task RedeemCode_GrantRevokedDuringTheRedemption_IsRefusedAndLeavesNoLiveToken()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = _ => OAuthApprovalDecision.Approve(new OAuthGrant { GrantId = "connection-3", Subject = "person-1", AuthType = "user" });
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync();
            server.Interceptor.BeforeNextRefreshTokenStore(() => server.RevokeGrantAsync("connection-3"));

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RedeemCodeAsync(authorization, code));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.CodeInvalidated, server.AuditLogger.Tokens.Single().FailureReason);
            Assert.IsTrue(server.Store.RefreshTokens.Single().IsInvalidated);
        }

        [TestMethod]
        public async Task Refresh_PresentedAgainDuringTheFirstRefresh_RefusesBothAndLeavesNoLiveToken()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            OAuthTestFlowException? second = null;
            server.Interceptor.BeforeNextRefreshTokenStore(async () =>
                second = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken)));

            OAuthTestFlowException first = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));

            Assert.AreEqual("invalid_grant", second?.Error);
            Assert.AreEqual("invalid_grant", first.Error);
            CollectionAssert.AreEqual(
                new OAuthTokenFailureReason?[] { OAuthTokenFailureReason.RefreshTokenReused, OAuthTokenFailureReason.RefreshTokenInvalidated },
                server.AuditLogger.Tokens.Skip(1).Select(result => result.FailureReason).ToArray());
            Assert.AreEqual(2, server.Store.RefreshTokens.Count);
            Assert.IsTrue(server.Store.RefreshTokens.All(token => token.IsInvalidated));
        }

        [TestMethod]
        public async Task Refresh_RevokedByTheClientDuringTheRefresh_IsRefusedAndLeavesNoLiveToken()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            server.Interceptor.BeforeNextRefreshTokenStore(() => server.OAuth.RevokeAsync(connected.Client, connected.Tokens.RefreshToken));

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.RefreshTokenInvalidated, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.AreEqual(2, server.Store.RefreshTokens.Count);
            Assert.IsTrue(server.Store.RefreshTokens.All(token => token.IsInvalidated));
        }

        [TestMethod]
        public async Task Refresh_GrantRevokedDuringTheRefresh_IsRefusedAndLeavesNoLiveToken()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = _ => OAuthApprovalDecision.Approve(new OAuthGrant { GrantId = "connection-4", Subject = "person-1", AuthType = "user" });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            server.Interceptor.BeforeNextRefreshTokenStore(() => server.RevokeGrantAsync("connection-4"));

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.RefreshTokenInvalidated, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.AreEqual(2, server.Store.RefreshTokens.Count);
            Assert.IsTrue(server.Store.RefreshTokens.All(token => token.IsInvalidated));
        }
    }
}
