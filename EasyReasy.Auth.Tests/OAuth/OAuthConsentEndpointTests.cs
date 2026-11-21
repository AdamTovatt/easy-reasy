using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Who may use the consent page's endpoints, and what they refuse: only a person signed in to the app (never a
    /// client's own token, never an API key's), and only for a pending request, with an approval body that is JSON.
    /// </summary>
    [TestClass]
    public class OAuthConsentEndpointTests
    {
        private const string RedirectUri = "https://client.example/callback";

        [TestMethod]
        [DataRow("GET", "")]
        [DataRow("POST", "/approve")]
        [DataRow("POST", "/deny")]
        public async Task ConsentEndpoint_WithAClientsAccessToken_Is403AndLeavesTheRequestPending(string method, string action)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(connected.Client, HostedOAuthServer.Resource);

            using HttpResponseMessage response = await SendAsync(server, new HttpMethod(method), authorization, action, connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.AreEqual(1, server.Handler.ApprovalCalls.Count);
            Assert.AreEqual(1, server.Store.AuthorizationCodes.Count);
            Assert.AreEqual(1, server.Store.AuthorizationRequests.Count(request => request.CompletedAt == null));
        }

        [TestMethod]
        public async Task Approve_WithAnApiKeysToken_Is403()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.ApproveAsync(authorization, server.SignIn("key-1", authType: "apikey")));

            Assert.AreEqual(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.AreEqual(0, server.Handler.ApprovalCalls.Count);
        }

        [TestMethod]
        public async Task GetRequest_WithoutTheAppsSignIn_Is401()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);

            using HttpResponseMessage response = await SendAsync(server, HttpMethod.Get, authorization, "", accessToken: null);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task GetRequest_Expired_Is404()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().AuthorizationRequestLifetime);

            OAuthTestFlowException expired = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.GetRequestAsync(authorization, server.SignIn()));

            Assert.AreEqual(HttpStatusCode.NotFound, expired.StatusCode);
        }

        [TestMethod]
        public async Task GetRequest_Completed_Is404()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            await server.OAuth.DenyAsync(authorization, server.SignIn());

            OAuthTestFlowException completed = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.GetRequestAsync(authorization, server.SignIn()));

            Assert.AreEqual(HttpStatusCode.NotFound, completed.StatusCode);
        }

        [TestMethod]
        public async Task Deny_Twice_SecondIs404AndAuditedOnce()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            await server.OAuth.DenyAsync(authorization, server.SignIn());

            OAuthTestFlowException second = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.DenyAsync(authorization, server.SignIn()));

            Assert.AreEqual(HttpStatusCode.NotFound, second.StatusCode);
            Assert.AreEqual(1, server.AuditLogger.Authorizations.Count(result => result.Outcome == OAuthAuthorizationOutcome.Denied));
        }

        [TestMethod]
        public async Task Deny_Expired_Is404()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().AuthorizationRequestLifetime);

            OAuthTestFlowException expired = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.DenyAsync(authorization, server.SignIn()));

            Assert.AreEqual(HttpStatusCode.NotFound, expired.StatusCode);
            Assert.AreEqual(0, server.AuditLogger.Authorizations.Count);
        }

        [TestMethod]
        public async Task Approve_BodyNotJson_Is400AndTheHandlerIsNotAsked()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);

            using HttpResponseMessage response = await SendAsync(server, HttpMethod.Post, authorization, "/approve", server.SignIn(), "organization=org-7");

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), "must be JSON");
            Assert.AreEqual(0, server.Handler.ApprovalCalls.Count);
        }

        [TestMethod]
        public async Task Approve_BodyOverTheLimit_Is400AndTheHandlerIsNotAsked()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            string body = "{\"note\":\"" + new string('a', OAuthJson.MaxRequestBodyBytes) + "\"}";

            using HttpResponseMessage response = await SendAsync(server, HttpMethod.Post, authorization, "/approve", server.SignIn(), body);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), "must be JSON");
            Assert.AreEqual(0, server.Handler.ApprovalCalls.Count);
        }

        private static async Task<HttpResponseMessage> SendAsync(HostedOAuthServer server, HttpMethod method, OAuthTestAuthorization authorization, string action, string? accessToken, string? body = null)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, OAuthServerPaths.AuthorizationRequestsPrefix + "/" + authorization.RequestId + action);
            if (accessToken != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            return await server.Client.SendAsync(request);
        }
    }
}
