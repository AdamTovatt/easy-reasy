namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Every OAuth event reaches the audit logger with what the record needs, and a client's refresh never reaches the
    /// first-party refresh hook.
    /// </summary>
    [TestClass]
    public class OAuthAuditTests
    {
        [TestMethod]
        public async Task Connect_AuditsRegistrationApprovalAndEachTokenRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = _ => OAuthApprovalDecision.Approve(new OAuthGrant { GrantId = "connection-1", Subject = "person-1", AuthType = "user" });

            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            await server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken);

            OAuthClientRegistrationResult registration = server.AuditLogger.Registrations.Single();
            Assert.IsTrue(registration.Success);
            Assert.AreEqual(connected.Client.ClientId, registration.ClientId);
            Assert.AreEqual(OAuthClientAuthenticationMethod.None, registration.AuthenticationMethod);

            OAuthAuthorizationResult approval = server.AuditLogger.Authorizations.Single();
            Assert.AreEqual(OAuthAuthorizationOutcome.Approved, approval.Outcome);
            Assert.AreEqual(connected.Client.ClientId, approval.Client.ClientId);
            Assert.AreEqual("Test Agent", approval.Client.ClientName);
            Assert.AreEqual("connection-1", approval.GrantId);
            Assert.AreEqual("person-1", approval.Subject);

            Assert.AreEqual(2, server.AuditLogger.Tokens.Count);
            OAuthTokenResult redeemed = server.AuditLogger.Tokens[0];
            OAuthTokenResult refreshed = server.AuditLogger.Tokens[1];
            Assert.IsTrue(redeemed.Success);
            Assert.AreEqual(OAuthGrantType.AuthorizationCode, redeemed.GrantType);
            Assert.AreEqual("connection-1", redeemed.GrantId);
            Assert.AreEqual(server.Store.RefreshTokens.First().FamilyId, redeemed.FamilyId);
            Assert.IsTrue(refreshed.Success);
            Assert.AreEqual(OAuthGrantType.RefreshToken, refreshed.GrantType);
            Assert.AreEqual("connection-1", refreshed.GrantId);
            Assert.AreEqual("person-1", refreshed.Subject);
            Assert.AreEqual(redeemed.FamilyId, refreshed.FamilyId);

            Assert.AreEqual(0, server.AuditLogger.FirstPartyRefreshes.Count);
        }

        [TestMethod]
        public async Task Approve_RefusedThenDenied_AuditsBothOutcomes()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            server.Handler.Approve = _ => OAuthApprovalDecision.Refuse("No.");

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.ApproveAsync(authorization, server.SignIn()));
            await server.OAuth.DenyAsync(authorization, server.SignIn());

            Assert.AreEqual(System.Net.HttpStatusCode.Forbidden, refused.StatusCode);

            Assert.AreEqual(OAuthAuthorizationOutcome.Refused, server.AuditLogger.Authorizations[0].Outcome);
            Assert.AreEqual("No.", server.AuditLogger.Authorizations[0].RefusalDetail);
            Assert.AreEqual(OAuthAuthorizationOutcome.Denied, server.AuditLogger.Authorizations[1].Outcome);
            Assert.IsNull(server.AuditLogger.Authorizations[1].GrantId);
        }

        [TestMethod]
        public async Task Refresh_HandlerThrows_IsAuditedAndPropagates()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            server.Handler.Refresh = _ => throw new InvalidOperationException("database down");

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));

            OAuthTokenResult fault = server.AuditLogger.Tokens.Last();
            Assert.AreEqual(OAuthTokenFailureReason.HandlerError, fault.FailureReason);
            Assert.AreEqual(server.Store.RefreshTokens.Single().GrantId, fault.GrantId);
            Assert.IsNull(server.Store.RefreshTokens.Single().ConsumedAt);
        }
    }
}
