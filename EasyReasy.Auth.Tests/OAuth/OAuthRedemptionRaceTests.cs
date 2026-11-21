namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A redemption that another request lands in the middle of, just before its refresh token is stored: a second
    /// presentation of the code, or a revocation of the grant. Nothing the first redemption issues may outlive either.
    /// </summary>
    [TestClass]
    public class OAuthRedemptionRaceTests
    {
        private const string RedirectUri = "https://client.example/callback";

        [TestMethod]
        public async Task RedeemCode_PresentedAgainDuringTheFirstRedemption_RefusesBothAndLeavesNoLiveToken()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            string code = await server.OAuth.ApproveAsync(authorization, server.SignIn());
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
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            string code = await server.OAuth.ApproveAsync(authorization, server.SignIn());
            server.Interceptor.BeforeNextRefreshTokenStore(() => server.RevokeGrantAsync("connection-3"));

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RedeemCodeAsync(authorization, code));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.CodeInvalidated, server.AuditLogger.Tokens.Single().FailureReason);
            Assert.IsTrue(server.Store.RefreshTokens.Single().IsInvalidated);
        }
    }
}
