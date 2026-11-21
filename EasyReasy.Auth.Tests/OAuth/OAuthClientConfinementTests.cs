using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A connection's access token is issued for the resource, its audience <see cref="OAuthServerOptions.Resource"/>, and
    /// is valid only there: with that audience, at an endpoint the app opened to connections. Anywhere else the bearer
    /// rejects it, however the app registered EasyReasy.Auth and wired its pipeline.
    /// </summary>
    [TestClass]
    public class OAuthClientConfinementTests
    {
        [TestMethod]
        public async Task ConnectionToken_EndpointNotOpenedToConnections_Is401InvalidToken()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.GetAsync(HostedOAuthServer.FirstPartyOnlyPath, connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            StringAssert.Contains(response.Headers.WwwAuthenticate.ToString(), "invalid_token");
        }

        [TestMethod]
        public async Task FirstPartyToken_EndpointNotOpenedToConnections_IsAllowed()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            using HttpResponseMessage response = await server.GetAsync(HostedOAuthServer.FirstPartyOnlyPath, server.SignIn());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [DataRow(HostedOAuthServer.MePath, DisplayName = "AllowOAuthClients()")]
        [DataRow(HostedOAuthServer.AttributeOpenedPath, DisplayName = "[AllowOAuthClients]")]
        [DataRow(HostedOAuthServer.OpenedGroupPath + "/projects", DisplayName = "a route group's AllowOAuthClients()")]
        public async Task ConnectionToken_EndpointOpenedToConnections_IsAllowed(string path)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.GetAsync(path, connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task ConnectionToken_OpenedEndpointWhosePolicyItFails_Is403()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.GetAsync(HostedOAuthServer.OpenedAdminPath, connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [TestMethod]
        public async Task ConnectionToken_OpenedEndpointWhosePolicyItMeets_IsAllowed()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = context => OAuthApprovalDecision.Approve(new OAuthGrant { GrantId = "connection-6", Subject = "person-1", AuthType = "user", Roles = ["admin"] });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.GetAsync(HostedOAuthServer.OpenedAdminPath, connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [DataRow("GET", "/.well-known/oauth-authorization-server", DisplayName = "authorization server metadata")]
        [DataRow("GET", "/.well-known/oauth-protected-resource/mcp", DisplayName = "protected resource metadata")]
        [DataRow("POST", "/oauth/register", DisplayName = "registration")]
        [DataRow("GET", "/oauth/authorize", DisplayName = "authorization")]
        [DataRow("POST", "/oauth/token", DisplayName = "token")]
        [DataRow("POST", "/oauth/revoke", DisplayName = "revocation")]
        public async Task ConnectionToken_TheServersOwnAnonymousEndpoints_AnswerAsWithoutIt(string method, string path)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            HttpStatusCode withToken = await SendAsync(server, method, path, connected.Tokens.AccessToken);
            HttpStatusCode withoutToken = await SendAsync(server, method, path, accessToken: null);

            Assert.AreEqual(withoutToken, withToken);
        }

        [TestMethod]
        public async Task ConnectionToken_IsIssuedForTheResource()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            OAuthTestTokens refreshed = await server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            CollectionAssert.AreEqual(new[] { HostedOAuthServer.Resource }, handler.ReadJwtToken(connected.Tokens.AccessToken).Audiences.ToArray());
            CollectionAssert.AreEqual(new[] { HostedOAuthServer.Resource }, handler.ReadJwtToken(refreshed.AccessToken).Audiences.ToArray());
        }

        [TestMethod]
        public async Task FirstPartyToken_WithTheServerRegistered_KeepsTheAppsAudience()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            IJwtTokenService tokens = server.App.Services.GetRequiredService<IJwtTokenService>();

            string token = tokens.CreateToken("person-1", "user", [], [], server.Clock.GetUtcNow().UtcDateTime.AddMinutes(5));

            CollectionAssert.AreEqual(new[] { HostedOAuthServer.Audience }, new JwtSecurityTokenHandler().ReadJwtToken(token).Audiences.ToArray());
        }

        [TestMethod]
        [DataRow(HostedOAuthServer.Audience, DisplayName = "the app sets an Audience")]
        [DataRow(null, DisplayName = "the app sets no Audience")]
        public async Task ConnectionToken_ForAnotherAudience_Is401AndForTheResourceIsAllowed(string? appAudience)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(setup: new HostedOAuthServerSetup { Audience = appAudience });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage forOther = await server.GetMeAsync(ForgeConnectionToken(server, connected, "https://other.example/mcp"));
            using HttpResponseMessage forResource = await server.GetMeAsync(ForgeConnectionToken(server, connected, HostedOAuthServer.Resource));

            Assert.AreEqual(HttpStatusCode.Unauthorized, forOther.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, forResource.StatusCode);
        }

        [TestMethod]
        public async Task FirstPartyToken_ForTheResource_IsHeldToTheAppsAudience()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            JwtTokenService resourceAudience = new JwtTokenService(HostedOAuthServer.Secret, HostedOAuthServer.JwtIssuer, HostedOAuthServer.Resource, server.Clock);
            string token = resourceAudience.CreateToken("person-1", "user", [], [], server.Clock.GetUtcNow().UtcDateTime.AddMinutes(5));

            using HttpResponseMessage response = await server.GetAsync(HostedOAuthServer.FirstPartyOnlyPath, token);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        [DataRow(HostedOAuthServerPipeline.AuthenticationAndAuthorizationByHand, HttpStatusCode.OK, DisplayName = "without UseEasyReasyAuth")]
        [DataRow(HostedOAuthServerPipeline.RoutingAfterAuthentication, HttpStatusCode.Unauthorized, DisplayName = "routing after authentication")]
        public async Task ConnectionToken_PipelineWiredAnotherWay_IsStillConfined(HostedOAuthServerPipeline pipeline, HttpStatusCode atAnOpenedEndpoint)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(setup: new HostedOAuthServerSetup { Pipeline = pipeline });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage closed = await server.GetAsync(HostedOAuthServer.FirstPartyOnlyPath, connected.Tokens.AccessToken);
            using HttpResponseMessage opened = await server.GetMeAsync(connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.Unauthorized, closed.StatusCode);
            Assert.AreEqual(atAnOpenedEndpoint, opened.StatusCode);
        }

        [TestMethod]
        [DataRow(false, true, DisplayName = "no IJwtTokenService registered")]
        [DataRow(true, false, DisplayName = "the server registered before AddEasyReasyAuth")]
        public async Task Connect_AppRegisteredAnotherWay_IssuesTokensForTheResourceAndConfinesThem(bool registerJwtTokenService, bool oauthServerRegisteredFirst)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(setup: new HostedOAuthServerSetup
            {
                RegisterJwtTokenService = registerJwtTokenService,
                OAuthServerRegisteredFirst = oauthServerRegisteredFirst,
            });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage opened = await server.GetMeAsync(connected.Tokens.AccessToken);
            using HttpResponseMessage closed = await server.GetAsync(HostedOAuthServer.FirstPartyOnlyPath, connected.Tokens.AccessToken);
            using HttpResponseMessage forOther = await server.GetMeAsync(ForgeConnectionToken(server, connected, "https://other.example/mcp"));

            Assert.AreEqual(HttpStatusCode.OK, opened.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, closed.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, forOther.StatusCode);
        }

        // A token the server would issue for this connection but for its audience, so the audience is the only difference.
        private static string ForgeConnectionToken(HostedOAuthServer server, HostedOAuthServer.ConnectedClient connected, string audience)
        {
            JwtSecurityToken issued = new JwtSecurityTokenHandler().ReadJwtToken(connected.Tokens.AccessToken);
            JwtTokenService tokens = new JwtTokenService(HostedOAuthServer.Secret, HostedOAuthServer.JwtIssuer, audience, server.Clock);
            return tokens.CreateToken(
                issued.Subject,
                "user",
                issued.Claims.Where(claim => claim.Type == EasyReasyClaimTypes.ClientId || claim.Type == EasyReasyClaimTypes.RefreshFamilyId || claim.Type == "organization_id"),
                ["member"],
                server.Clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        }

        private static async Task<HttpStatusCode> SendAsync(HostedOAuthServer server, string method, string path, string? accessToken)
        {
            using HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), path);
            if (method == "POST")
            {
                request.Content = new FormUrlEncodedContent([]);
            }

            if (accessToken != null)
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            using HttpResponseMessage response = await server.Client.SendAsync(request);
            return response.StatusCode;
        }
    }
}
