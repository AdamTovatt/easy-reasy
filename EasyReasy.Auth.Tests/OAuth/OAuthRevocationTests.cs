using System.Net;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The revocation endpoint (RFC 7009): a client's own refresh token ends its family, anything else is answered 200 and
    /// left alone, and a malformed or unauthenticated request is refused.
    /// </summary>
    [TestClass]
    public class OAuthRevocationTests
    {
        private const string RedirectUri = "https://client.example/callback";

        [TestMethod]
        public async Task Revoke_OwnRefreshToken_EndsItsFamily()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            await server.OAuth.RevokeAsync(connected.Client, connected.Tokens.RefreshToken);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));
            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.RefreshTokenInvalidated, server.AuditLogger.Tokens.Last().FailureReason);
            OAuthRevocationResult revocation = server.AuditLogger.Revocations.Single();
            Assert.IsTrue(revocation.WasKnown);
            Assert.AreEqual(connected.Client.ClientId, revocation.ClientId);
        }

        [TestMethod]
        public async Task Revoke_AnotherClientsToken_Is200AndLeavesItUsable()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            OAuthTestRegisteredClient other = await server.OAuth.RegisterAsync(RedirectUri);

            await server.OAuth.RevokeAsync(other, connected.Tokens.RefreshToken);
            await server.OAuth.RevokeAsync(other, "unknown-token");

            Assert.IsFalse(server.Store.RefreshTokens.Single().IsInvalidated);
            Assert.AreEqual(2, server.AuditLogger.Revocations.Count);
            Assert.IsTrue(server.AuditLogger.Revocations.All(revocation => !revocation.WasKnown));
        }

        [TestMethod]
        public async Task Revoke_WrongClientSecret_Is401AndLeavesTheTokenUsable()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri, OAuthClientAuthenticationMethod.ClientSecretPost);
            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RevokeAsync(client with { ClientSecret = "wrong" }, tokens.RefreshToken));

            Assert.AreEqual(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
            Assert.IsFalse(server.Store.RefreshTokens.Single().IsInvalidated);
        }

        [TestMethod]
        public async Task Revoke_NoToken_IsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            using HttpResponseMessage response = await server.PostFormAsync(OAuthServerPaths.Revoke, [new KeyValuePair<string, string>("client_id", client.ClientId)]);

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
            Assert.AreEqual(0, server.AuditLogger.Revocations.Count);
        }

        [TestMethod]
        public async Task Revoke_BodyNotAForm_IsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            string body = "{\"client_id\":\"" + connected.Client.ClientId + "\",\"token\":\"" + connected.Tokens.RefreshToken + "\"}";

            using HttpResponseMessage response = await server.Client.PostAsync(OAuthServerPaths.Revoke, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
            Assert.IsFalse(server.Store.RefreshTokens.Single().IsInvalidated);
        }
    }
}
