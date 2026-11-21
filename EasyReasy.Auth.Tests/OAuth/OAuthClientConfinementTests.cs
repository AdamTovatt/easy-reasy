using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A connection's access token is issued for the resource, its audience <see cref="OAuthServerOptions.Resource"/>,
    /// signed with a key of its own and validated by a scheme of its own, which only endpoints the app opened to
    /// connections run. Anywhere else the app's bearer rejects it, however the app registered EasyReasy.Auth, configured
    /// its audience and wired its pipeline.
    /// </summary>
    [TestClass]
    public class OAuthClientConfinementTests
    {
        [TestMethod]
        [DataRow(HostedOAuthServer.Audience, DisplayName = "the app sets an Audience")]
        [DataRow(null, DisplayName = "the app sets no Audience")]
        public async Task ConnectionToken_EndpointNotOpenedToConnections_Is401InvalidToken(string? appAudience)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(setup: new HostedOAuthServerSetup { Audience = appAudience });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.GetAsync(HostedOAuthServer.FirstPartyOnlyPath, connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            StringAssert.Contains(response.Headers.WwwAuthenticate.ToString(), "invalid_token");
        }

        [TestMethod]
        public async Task ConnectionToken_ExpiredAtAnOpenedEndpoint_Is401WithOneChallengeSayingItExpired()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().AccessTokenLifetime + TimeSpan.FromMinutes(5));

            using HttpResponseMessage response = await server.GetMeAsync(connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            string challenge = SingleChallengeNamingTheResourceMetadata(response);
            StringAssert.Contains(challenge, "error=\"invalid_token\"");
            StringAssert.Contains(challenge, "expired");
        }

        [TestMethod]
        public async Task FirstPartyToken_ExpiredAtAnOpenedEndpoint_Is401WithOneChallengeSayingItExpired()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            string signIn = server.SignIn();
            server.Clock.Advance(TimeSpan.FromMinutes(10));

            using HttpResponseMessage response = await server.GetMeAsync(signIn);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            StringAssert.Contains(SingleChallengeNamingTheResourceMetadata(response), "expired");
        }

        [TestMethod]
        public async Task ConnectionToken_EndpointOpenedWithNoPolicyOfItsOwn_RequiresAnAuthenticatedCaller()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage withToken = await server.GetAsync(HostedOAuthServer.OpenedWithoutPolicyPath, connected.Tokens.AccessToken);
            using HttpResponseMessage anonymous = await server.Client.GetAsync(HostedOAuthServer.OpenedWithoutPolicyPath);

            Assert.AreEqual(HttpStatusCode.OK, withToken.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            SingleChallengeNamingTheResourceMetadata(anonymous);
        }

        [TestMethod]
        public async Task OpenedEndpoint_AppsBearerWritesItsOwnChallengeBody_Is401WithThatBody()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(setup: new HostedOAuthServerSetup { AppBearerWritesItsOwnBody = true });

            using HttpResponseMessage response = await server.Client.GetAsync(HostedOAuthServer.MePath);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), HostedOAuthServer.AppBearerChallengeError);
        }

        [TestMethod]
        public async Task OpenedEndpoint_AppsBearerWritesItsOwnForbiddenBody_Is403WithThatBody()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(setup: new HostedOAuthServerSetup { AppBearerWritesItsOwnBody = true });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await server.GetAsync(HostedOAuthServer.OpenedAdminPath, connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), HostedOAuthServer.AppBearerForbiddenError);
        }

        [TestMethod]
        public async Task ConnectionToken_ControllerOpenedAsAWhole_ReachesItsActionsAsTheirOwnAuthorizationAllows()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage plain = await server.GetAsync(OpenedConnectionsController.PlainPath, connected.Tokens.AccessToken);
            using HttpResponseMessage admin = await server.GetAsync(OpenedConnectionsController.AdminPath, connected.Tokens.AccessToken);
            using HttpResponseMessage anonymous = await server.Client.GetAsync(OpenedConnectionsController.PlainPath);

            Assert.AreEqual(HttpStatusCode.OK, plain.StatusCode);
            StringAssert.Contains(await plain.Content.ReadAsStringAsync(), "person-1");
            Assert.AreEqual(HttpStatusCode.Forbidden, admin.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        [TestMethod]
        public async Task ConnectionToken_ControllerActionWhoseRoleItHas_IsAllowed()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = context => OAuthApprovalDecision.Approve(new OAuthGrant { GrantId = "connection-8", Subject = "person-1", AuthType = "user", Roles = ["admin"] });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage admin = await server.GetAsync(OpenedConnectionsController.AdminPath, connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.OK, admin.StatusCode);
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
        public async Task FirstPartyToken_EndpointOpenedToConnections_ReachesItAsThePerson()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            using HttpResponseMessage response = await server.GetMeAsync(server.SignIn("person-4"));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument me = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual("person-4", me.RootElement.GetProperty("sub").GetString());
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
        public async Task ConnectionToken_FromAnotherIssuer_Is401AndFromTheAppsIssuerIsAllowed()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            byte[] secretKey = JwtTokenService.SigningKeyFromSecret(HostedOAuthServer.Secret, "secret");
            JwtSigningSettings otherIssuer = new JwtSigningSettings(secretKey, "https://other-issuer.example", TimeSpan.Zero);
            JwtSigningSettings appsIssuer = new JwtSigningSettings(secretKey, HostedOAuthServer.JwtIssuer, TimeSpan.Zero);

            using HttpResponseMessage fromOther = await server.GetMeAsync(CopyConnectionToken(connected, otherIssuer.CreateConnectionTokenService(HostedOAuthServer.Resource, server.Clock), server));
            using HttpResponseMessage fromApps = await server.GetMeAsync(CopyConnectionToken(connected, appsIssuer.CreateConnectionTokenService(HostedOAuthServer.Resource, server.Clock), server));

            Assert.AreEqual(HttpStatusCode.Unauthorized, fromOther.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, fromApps.StatusCode);
        }

        [TestMethod]
        public async Task TokenSignedWithTheAppsKey_ForTheResourceAtAnOpenedEndpoint_Is401()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            JwtTokenService appKeyForTheResource = new JwtTokenService(HostedOAuthServer.Secret, HostedOAuthServer.JwtIssuer, HostedOAuthServer.Resource, server.Clock);

            using HttpResponseMessage response = await server.GetMeAsync(CopyConnectionToken(connected, appKeyForTheResource, server));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        [DataRow(HostedOAuthServerPipeline.AuthenticationAndAuthorizationByHand, DisplayName = "without UseEasyReasyAuth")]
        [DataRow(HostedOAuthServerPipeline.RoutingAfterAuthentication, DisplayName = "routing after authentication")]
        public async Task ConnectionToken_PipelineWiredAnotherWay_IsConfinedTheSameWay(HostedOAuthServerPipeline pipeline)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(setup: new HostedOAuthServerSetup { Pipeline = pipeline });
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage closed = await server.GetAsync(HostedOAuthServer.FirstPartyOnlyPath, connected.Tokens.AccessToken);
            using HttpResponseMessage opened = await server.GetMeAsync(connected.Tokens.AccessToken);

            Assert.AreEqual(HttpStatusCode.Unauthorized, closed.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, opened.StatusCode);
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
            return CopyConnectionToken(connected, server.Signing.CreateConnectionTokenService(audience, server.Clock), server);
        }

        // The response's one WWW-Authenticate challenge, which must name the protected resource metadata.
        private static string SingleChallengeNamingTheResourceMetadata(HttpResponseMessage response)
        {
            Assert.AreEqual(1, response.Headers.WwwAuthenticate.Count, response.Headers.WwwAuthenticate.ToString());
            string challenge = response.Headers.WwwAuthenticate.Single().ToString();
            StringAssert.Contains(challenge, "resource_metadata=");
            return challenge;
        }

        // The connection's token's subject and claims, issued again by another token service.
        private static string CopyConnectionToken(HostedOAuthServer.ConnectedClient connected, JwtTokenService tokens, HostedOAuthServer server)
        {
            JwtSecurityToken issued = new JwtSecurityTokenHandler().ReadJwtToken(connected.Tokens.AccessToken);
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
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            using HttpResponseMessage response = await server.Client.SendAsync(request);
            return response.StatusCode;
        }
    }
}
