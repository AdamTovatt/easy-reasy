using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Claims;

namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// <see cref="OAuthTestClient"/> and <see cref="InMemoryOAuthServerStore"/> drive a real EasyReasy.Auth OAuth server the
    /// way a consumer's test host does: every step of a connection, a redirect URI other than the registered one, and a
    /// refusal surfaced as <see cref="OAuthTestFlowException"/>.
    /// </summary>
    [TestClass]
    public class OAuthTestClientTests
    {
        private const string Issuer = "https://app.example";
        private const string Resource = "https://app.example/mcp";

        [TestMethod]
        public async Task ConnectRefreshRevoke_PublicClient_RunsTheWholeConnection()
        {
            await using WebApplication app = await StartAsync();
            OAuthTestClient oauth = new OAuthTestClient(Client(app));
            OAuthTestRegisteredClient client = await oauth.RegisterAsync("https://client.example/callback", clientName: "Agent");

            OAuthTestTokens tokens = await oauth.ConnectAsync(client, SignIn(app), new { organization = "org-7" }, Resource);
            OAuthTestTokens refreshed = await oauth.RefreshAsync(client, tokens.RefreshToken);
            await oauth.RevokeAsync(client, refreshed.RefreshToken);

            Assert.AreEqual("Bearer", tokens.TokenType);
            Assert.AreNotEqual(tokens.RefreshToken, refreshed.RefreshToken);
            InMemoryOAuthServerStore store = app.Services.GetRequiredService<InMemoryOAuthServerStore>();
            Assert.AreEqual("Agent", store.Clients.Single().ClientName);
            Assert.AreEqual(2, store.RefreshTokens.Count);
            Assert.IsTrue(store.RefreshTokens.All(token => token.IsInvalidated));
        }

        [TestMethod]
        public async Task AuthorizeAsync_RedirectUriOverride_IsUsedThroughApprovalAndRedemption()
        {
            await using WebApplication app = await StartAsync();
            OAuthTestClient oauth = new OAuthTestClient(Client(app));
            OAuthTestRegisteredClient client = await oauth.RegisterAsync("http://127.0.0.1/callback");

            OAuthTestAuthorization authorization = await oauth.AuthorizeAsync(client, redirectUri: "http://127.0.0.1:8123/callback");
            string code = await oauth.ApproveAsync(authorization, SignIn(app));
            OAuthTestTokens tokens = await oauth.RedeemCodeAsync(authorization, code);

            Assert.AreEqual("http://127.0.0.1:8123/callback", authorization.RedirectUri);
            Assert.AreEqual("http://127.0.0.1:8123/callback", app.Services.GetRequiredService<InMemoryOAuthServerStore>().AuthorizationCodes.Single().RedirectUri);
            Assert.AreEqual("Bearer", tokens.TokenType);
        }

        [TestMethod]
        public async Task RedeemCodeAsync_WrongVerifier_ThrowsWithTheOAuthError()
        {
            await using WebApplication app = await StartAsync();
            OAuthTestClient oauth = new OAuthTestClient(Client(app));
            OAuthTestRegisteredClient client = await oauth.RegisterAsync("https://client.example/callback");
            OAuthTestAuthorization authorization = await oauth.AuthorizeAsync(client);
            string code = await oauth.ApproveAsync(authorization, SignIn(app));

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => oauth.RedeemCodeAsync(authorization, code, OAuthTestPkce.CreateVerifier()));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_grant", refused.Error);
        }

        [TestMethod]
        public void ParseQuery_DecodesEachParameterAndKeepsTheLastValue()
        {
            Dictionary<string, string> parameters = OAuthTestClient.ParseQuery("https://client.example/cb?code=a%2Bb&state=x+y&state=z&empty=");

            Assert.AreEqual("a+b", parameters["code"]);
            Assert.AreEqual("z", parameters["state"]);
            Assert.AreEqual("", parameters["empty"]);
            Assert.AreEqual(0, OAuthTestClient.ParseQuery("/no-query").Count);
        }

        private static async Task<WebApplication> StartAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddEasyReasyAuth("super_secret_key_12345_12345_12345", options =>
            {
                options.Issuer = "test-issuer";
                options.Audience = "test-api";
            });
            builder.Services.AddEasyReasyOAuthServer<InMemoryOAuthServerStore, PersonGrantHandler>(new OAuthServerOptions
            {
                IssuerUrl = Issuer,
                Resource = Resource,
                ConsentPageUrl = "/connect",
            });
            InMemoryOAuthServerStore store = new InMemoryOAuthServerStore();
            builder.Services.AddSingleton(store);
            builder.Services.AddSingleton<IOAuthServerStore>(store);

            WebApplication app = builder.Build();
            app.UseEasyReasyAuth(delay => delay.Enabled = false);
            app.AddOAuthServerEndpoints();
            await app.StartAsync();
            return app;
        }

        private static HttpClient Client(WebApplication app)
        {
            HttpClient client = ((TestServer)app.Services.GetRequiredService<IServer>()).CreateClient();
            client.BaseAddress = new Uri(Issuer);
            return client;
        }

        private static string SignIn(WebApplication app)
        {
            return app.Services.GetRequiredService<IJwtTokenService>().CreateToken("person-1", "user", Array.Empty<Claim>(), [], DateTime.UtcNow.AddMinutes(5));
        }

        private sealed class PersonGrantHandler : IOAuthGrantHandler
        {
            public Task<OAuthApprovalDecision> ApproveAsync(OAuthApprovalContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(OAuthApprovalDecision.Approve(new OAuthGrant
                {
                    GrantId = Guid.NewGuid().ToString("N"),
                    Subject = context.HttpContext.GetUserId()!,
                    AuthType = "user",
                }));
            }

            public Task<RefreshClaimsDecision> RefreshAsync(OAuthRefreshContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(RefreshClaimsDecision.Allow(context.StoredClaims, context.StoredRoles));
            }
        }
    }
}
