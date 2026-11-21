using System.Net;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the token endpoint refuses: a wrong verifier, a reused code (which invalidates what it issued), a reused
    /// refresh token (which invalidates its family), another client's code or token, a revoked grant, and malformed
    /// requests.
    /// </summary>
    [TestClass]
    public class OAuthTokenRefusalTests
    {
        private const string RedirectUri = "https://client.example/callback";

        [TestMethod]
        public async Task RedeemCode_WrongVerifier_IsInvalidGrantAndUsesTheCodeUp()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync();

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RedeemCodeAsync(authorization, code, codeVerifier: OAuthTestPkce.CreateVerifier()));
            OAuthTestFlowException retried = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RedeemCodeAsync(authorization, code));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual("invalid_grant", retried.Error);
            Assert.AreEqual(0, server.Store.RefreshTokens.Count);
            Assert.AreEqual(OAuthTokenFailureReason.VerifierMismatch, server.AuditLogger.Tokens[0].FailureReason);
        }

        [TestMethod]
        public async Task RedeemCode_Reused_IsInvalidGrantAndInvalidatesWhatTheFirstRedemptionIssued()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            OAuthTestFlowException reused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RedeemCodeAsync(connected.Authorization, connected.Code));

            Assert.AreEqual("invalid_grant", reused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.CodeReused, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.IsTrue(server.Store.AuthorizationCodes.Single().IsInvalidated);
            OAuthTestFlowException refresh = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));
            Assert.AreEqual("invalid_grant", refresh.Error);
            Assert.AreEqual(OAuthTokenFailureReason.RefreshTokenInvalidated, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        public async Task Refresh_Reused_IsInvalidGrantAndInvalidatesTheFamily()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            OAuthTestTokens rotated = await server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken);

            OAuthTestFlowException reused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));

            Assert.AreEqual("invalid_grant", reused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.RefreshTokenReused, server.AuditLogger.Tokens.Last().FailureReason);
            OAuthTestFlowException rotatedAfter = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(connected.Client, rotated.RefreshToken));
            Assert.AreEqual("invalid_grant", rotatedAfter.Error);
            Assert.IsTrue(server.Store.RefreshTokens.All(token => token.IsInvalidated));
        }

        [TestMethod]
        public async Task RedeemCode_RedirectUriOtherThanTheRequests_IsInvalidGrant()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync("http://127.0.0.1/callback", redirectUri: "http://127.0.0.1:5000/callback");

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RedeemCodeAsync(authorization with { RedirectUri = "http://127.0.0.1/callback" }, code));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.RedirectUriMismatch, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        public async Task RedeemCode_ByAnotherClient_IsInvalidGrant()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync();
            OAuthTestRegisteredClient other = await server.OAuth.RegisterAsync(RedirectUri);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RedeemCodeAsync(authorization with { Client = other }, code));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.ClientMismatch, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        public async Task RedeemCode_Expired_IsInvalidGrant()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync();
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().AuthorizationCodeLifetime);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RedeemCodeAsync(authorization, code));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.CodeExpired, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        public async Task RedeemCode_ResourceOtherThanTheRequests_IsInvalidTarget()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync(resource: HostedOAuthServer.Resource);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RedeemCodeAsync(authorization with { Resource = HostedOAuthServer.Resource + "/other" }, code));

            Assert.AreEqual("invalid_target", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.ResourceMismatch, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        public async Task RedeemCode_ResourceNotThisServers_IsInvalidTargetAndLeavesTheCodeUnused()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync(resource: HostedOAuthServer.Resource);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RedeemCodeAsync(authorization with { Resource = "https://other.example/mcp" }, code));

            Assert.AreEqual("invalid_target", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.ResourceMismatch, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.IsNull(server.Store.AuthorizationCodes.Single().RedeemedAt);
        }

        [TestMethod]
        public async Task Refresh_ResourceNotThisServers_IsInvalidTargetAndLeavesTheTokenUnused()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.PostFormAsync(OAuthServerPaths.Token, RefreshForm(connected, "https://other.example/mcp"));

            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_target");
            Assert.AreEqual(OAuthTokenFailureReason.ResourceMismatch, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.IsNull(server.Store.RefreshTokens.Single().ConsumedAt);
        }

        [TestMethod]
        public async Task Refresh_ThisServersResource_Rotates()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.PostFormAsync(OAuthServerPaths.Token, RefreshForm(connected, HostedOAuthServer.Resource));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(server.AuditLogger.Tokens.Last().Success);
            Assert.AreEqual(2, server.Store.RefreshTokens.Count);
        }

        [TestMethod]
        public async Task RedeemCode_UnknownCode_IsInvalidGrant()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RedeemCodeAsync(authorization, "not-a-code"));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.CodeNotFound, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        public async Task Refresh_AnotherClientsToken_IsInvalidGrantAndLeavesItUsable()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            OAuthTestRegisteredClient other = await server.OAuth.RegisterAsync(RedirectUri);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(other, connected.Tokens.RefreshToken));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.RefreshTokenNotFound, server.AuditLogger.Tokens.Last().FailureReason);
            OAuthTestTokens refreshed = await server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken);
            Assert.AreNotEqual(connected.Tokens.RefreshToken, refreshed.RefreshToken);
        }

        [TestMethod]
        public async Task Refresh_HandlerDenies_IsInvalidGrantAndDoesNotUseTheTokenUp()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            server.Handler.Refresh = _ => RefreshClaimsDecision.Deny();

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.DeniedByHandler, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.IsNull(server.Store.RefreshTokens.Single().ConsumedAt);
        }

        [TestMethod]
        public async Task Refresh_Expired_IsInvalidGrant()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().RefreshTokenLifetime);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken));

            Assert.AreEqual("invalid_grant", refused.Error);
            Assert.AreEqual(OAuthTokenFailureReason.RefreshTokenExpired, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        public async Task RevokeGrant_EndsEveryFamilyUnderIt()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = _ => OAuthApprovalDecision.Approve(new OAuthGrant { GrantId = "connection-1", Subject = "person-1", AuthType = "user" });
            HostedOAuthServer.ConnectedClient first = await server.ConnectPublicClientAsync();
            OAuthTestTokens second = await server.OAuth.ConnectAsync(first.Client, server.SignIn());

            await server.RevokeGrantAsync("connection-1");

            OAuthTestFlowException firstRefused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RefreshAsync(first.Client, first.Tokens.RefreshToken));
            OAuthTestFlowException secondRefused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RefreshAsync(first.Client, second.RefreshToken));
            Assert.AreEqual(OAuthTokenFailureReason.RefreshTokenInvalidated, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.AreEqual("invalid_grant", firstRefused.Error);
            Assert.AreEqual("invalid_grant", secondRefused.Error);
        }

        [TestMethod]
        public async Task RevokeGrant_BeforeTheCodeIsRedeemed_RefusesTheCode()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = _ => OAuthApprovalDecision.Approve(new OAuthGrant { GrantId = "connection-2", Subject = "person-1", AuthType = "user" });
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync();
            await server.RevokeGrantAsync("connection-2");

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RedeemCodeAsync(authorization, code));

            Assert.AreEqual(OAuthTokenFailureReason.CodeInvalidated, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.AreEqual("invalid_grant", refused.Error);
        }

        [TestMethod]
        public async Task Refresh_ClientRefreshTokenAtTheFirstPartyEndpoint_Is401()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.Client.PostAsync(
                "/api/auth/refresh",
                new StringContent(new RefreshRequest(connected.Tokens.RefreshToken).ToJson(), System.Text.Encoding.UTF8, "application/json"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.IsNull(server.Store.RefreshTokens.Single().ConsumedAt);
        }

        [TestMethod]
        public async Task Token_BodyNotAForm_IsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            using HttpResponseMessage response = await server.Client.PostAsync(
                OAuthServerPaths.Token, new StringContent("{\"grant_type\":\"refresh_token\"}", System.Text.Encoding.UTF8, "application/json"));

            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
            Assert.AreEqual(OAuthTokenFailureReason.InvalidRequest, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        public async Task Token_RepeatedParameter_IsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.PostFormAsync(OAuthServerPaths.Token,
            [
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", connected.Client.ClientId),
                new KeyValuePair<string, string>("refresh_token", connected.Tokens.RefreshToken),
                new KeyValuePair<string, string>("refresh_token", connected.Tokens.RefreshToken),
            ]);

            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
            Assert.IsNull(server.Store.RefreshTokens.Single().ConsumedAt);
        }

        [TestMethod]
        [DataRow(null, "invalid_request", OAuthTokenFailureReason.InvalidRequest)]
        [DataRow("password", "unsupported_grant_type", OAuthTokenFailureReason.UnsupportedGrantType)]
        public async Task Token_GrantTypeMissingOrUnsupported_IsRefusedByWhich(string? grantType, string error, OAuthTokenFailureReason reason)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            List<KeyValuePair<string, string>> fields = [new KeyValuePair<string, string>("client_id", client.ClientId)];
            if (grantType != null)
            {
                fields.Add(new KeyValuePair<string, string>("grant_type", grantType));
            }

            using HttpResponseMessage response = await server.PostFormAsync(OAuthServerPaths.Token, fields);

            await AssertErrorAsync(response, HttpStatusCode.BadRequest, error);
            Assert.AreEqual(reason, server.AuditLogger.Tokens.Last().FailureReason);
        }

        [TestMethod]
        [DataRow("code")]
        [DataRow("redirect_uri")]
        [DataRow("code_verifier")]
        public async Task RedeemCode_ParameterMissing_IsInvalidRequestAndLeavesTheCodeUnused(string missing)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (OAuthTestRegisteredClient client, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync();
            Dictionary<string, string> fields = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = client.ClientId,
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = authorization.CodeVerifier,
            };
            fields.Remove(missing);

            using HttpResponseMessage response = await server.PostFormAsync(OAuthServerPaths.Token, fields);

            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
            Assert.IsNull(server.Store.AuthorizationCodes.Single().RedeemedAt);
        }

        [TestMethod]
        public async Task Refresh_TokenMissing_IsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            using HttpResponseMessage response = await server.PostFormAsync(OAuthServerPaths.Token,
            [
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", client.ClientId),
            ]);

            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
            Assert.AreEqual(OAuthTokenFailureReason.InvalidRequest, server.AuditLogger.Tokens.Last().FailureReason);
        }

        private static List<KeyValuePair<string, string>> RefreshForm(HostedOAuthServer.ConnectedClient connected, string resource)
        {
            return
            [
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", connected.Client.ClientId),
                new KeyValuePair<string, string>("refresh_token", connected.Tokens.RefreshToken),
                new KeyValuePair<string, string>("resource", resource),
            ];
        }

        internal static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode statusCode, string error)
        {
            Assert.AreEqual(statusCode, response.StatusCode);
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(error, document.RootElement.GetProperty("error").GetString());
        }
    }
}
