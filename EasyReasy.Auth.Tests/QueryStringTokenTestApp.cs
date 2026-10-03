using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A real <see cref="WebApplication"/> on a <see cref="TestServer"/> with an authorized <c>whoami</c> endpoint
    /// under several paths and an authorized WebSocket endpoint at <c>/ws/city</c>, each answering with the
    /// caller's user id, so a test can see which token, if any, authenticated a request.
    /// </summary>
    internal sealed class QueryStringTokenTestApp : IAsyncDisposable
    {
        public const string Secret = "super_secret_key_12345_12345_12345";

        public WebApplication App { get; }
        public TestServer Server { get; }
        public HttpClient Client { get; }

        private QueryStringTokenTestApp(WebApplication app, TestServer server)
        {
            App = app;
            Server = server;
            Client = server.CreateClient();
        }

        public static async Task<QueryStringTokenTestApp> StartAsync(
            IReadOnlyList<string> queryStringTokenPaths,
            Action<JwtBearerOptions>? configureBearer = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            builder.Services.AddEasyReasyAuth(Secret, options => options.QueryStringTokenPaths = queryStringTokenPaths);

            if (configureBearer != null)
            {
                builder.Services.Configure(JwtBearerDefaults.AuthenticationScheme, configureBearer);
            }

            WebApplication app = builder.Build();
            app.UseEasyReasyAuth(options => options.Enabled = false);

            // After authentication on purpose: the fallback matches on path, so it must not depend on the
            // WebSocket middleware having already marked the request as an upgrade.
            app.UseWebSockets();

            foreach (string path in new[] { "/ws/whoami", "/wsx/whoami", "/api/whoami" })
            {
                app.MapGet(path, (HttpContext context) => context.GetUserId()).RequireAuthorization();
            }

            app.Map("/ws/city", (RequestDelegate)AnswerWithUserIdOverWebSocketAsync).RequireAuthorization();

            await app.StartAsync();
            TestServer server = (TestServer)app.Services.GetRequiredService<IServer>();
            return new QueryStringTokenTestApp(app, server);
        }

        public static string CreateToken(string subject)
        {
            return new JwtTokenService(Secret).CreateToken(subject, "user", [], [], DateTime.UtcNow.AddMinutes(5));
        }

        public static string CreateExpiredToken(string subject)
        {
            DateTime now = DateTime.UtcNow;
            return WriteToken(subject, Secret, notBefore: now.AddMinutes(-10), expires: now.AddMinutes(-5));
        }

        public static string CreateTokenSignedWithOtherSecret(string subject)
        {
            DateTime now = DateTime.UtcNow;
            return WriteToken(subject, "another_secret_key_67890_67890_67890", notBefore: now, expires: now.AddMinutes(5));
        }

        public async Task<HttpResponseMessage> GetAsync(string pathAndQuery, string? authorizationHeader = null)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);

            if (authorizationHeader != null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
            }

            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Client.Dispose();
            }
            finally
            {
                await App.DisposeAsync();
            }
        }

        private static async Task AnswerWithUserIdOverWebSocketAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            byte[] payload = Encoding.UTF8.GetBytes(context.GetUserId() ?? "");
            await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        private static string WriteToken(string subject, string secret, DateTime notBefore, DateTime expires)
        {
            SigningCredentials credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token = new JwtSecurityToken(
                claims: [new Claim(JwtRegisteredClaimNames.Sub, subject), new Claim("auth_type", "user")],
                notBefore: notBefore,
                expires: expires,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
