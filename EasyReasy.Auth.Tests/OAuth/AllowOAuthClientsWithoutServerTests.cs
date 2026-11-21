using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// <c>[AllowOAuthClients]</c> in an app that never registered the OAuth server: the endpoint runs, the app's own tokens
    /// reach it, and no token signed as a connection's would be does.
    /// </summary>
    [TestClass]
    public class AllowOAuthClientsWithoutServerTests
    {
        private const string OpenedPath = "/api/opened";

        [TestMethod]
        public async Task OpenedEndpoint_FirstPartyToken_IsAllowed()
        {
            await using WebApplication app = await StartAsync();
            string token = app.Services.GetRequiredService<IJwtTokenService>().CreateToken("person-1", "user", [], [], DateTime.UtcNow.AddMinutes(5));

            using HttpResponseMessage response = await GetAsync(app, token);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task OpenedEndpoint_TokenSignedAsAConnectionsForAnyAudience_Is401()
        {
            await using WebApplication app = await StartAsync();
            JwtTokenService connectionKey = app.Services.GetRequiredService<JwtSigningSettings>().CreateConnectionTokenService("https://app.example/mcp", TimeProvider.System);
            string token = connectionKey.CreateToken("person-1", "user", [new System.Security.Claims.Claim(EasyReasyClaimTypes.ClientId, "client-1")], [], DateTime.UtcNow.AddMinutes(5));

            using HttpResponseMessage response = await GetAsync(app, token);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private static async Task<WebApplication> StartAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddEasyReasyAuth(HostedOAuthServer.Secret);

            WebApplication app = builder.Build();
            app.UseEasyReasyAuth(delay => delay.Enabled = false);
            app.MapGet(OpenedPath, () => Results.Ok()).RequireAuthorization().AllowOAuthClients();
            await app.StartAsync();
            return app;
        }

        private static async Task<HttpResponseMessage> GetAsync(WebApplication app, string token)
        {
            HttpClient client = ((TestServer)app.Services.GetRequiredService<IServer>()).CreateClient();
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, OpenedPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await client.SendAsync(request);
        }
    }
}
