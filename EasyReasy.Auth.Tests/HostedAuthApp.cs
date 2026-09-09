using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Helper that builds a real <see cref="WebApplication"/> with the library's own
    /// <see cref="AuthApplicationBuilderExtensions.AddAuthEndpoints"/> and exposes a
    /// <see cref="TestServer"/>-backed <see cref="HttpClient"/>, so endpoint behaviour can be
    /// asserted at the level it happens. Used across multiple test classes.
    /// </summary>
    internal sealed class HostedAuthApp : IAsyncDisposable
    {
        private const string Secret = "super_secret_key_12345_12345_12345";

        public WebApplication App { get; }
        public HttpClient Client { get; }

        private HostedAuthApp(WebApplication app, HttpClient client)
        {
            App = app;
            Client = client;
        }

        public static async Task<HostedAuthApp> StartAsync(
            IAuthAuditLogger? auditLogger,
            IAuthRequestValidationService validationService,
            IRefreshTokenStore? store = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            builder.Services.AddEasyReasyAuth(Secret);
            builder.Services.AddSingleton<IAuthRequestValidationService>(validationService);

            IRefreshTokenStore sharedStore = store ?? new FakeRefreshTokenStore();
            builder.Services.AddSingleton<IRefreshTokenStore>(sharedStore);
            builder.Services.AddSingleton<IRefreshTokenService>(provider =>
                new RefreshTokenService(
                    provider.GetRequiredService<IRefreshTokenStore>(),
                    auditLogger: provider.GetService<IAuthAuditLogger>()));

            if (auditLogger != null)
            {
                builder.Services.AddSingleton<IAuthAuditLogger>(auditLogger);
            }

            WebApplication app = builder.Build();
            app.UseEasyReasyAuth(options => options.Enabled = false);
            app.AddAuthEndpoints(allowRefresh: true, allowLogout: true);

            await app.StartAsync();
            TestServer server = (TestServer)app.Services.GetRequiredService<IServer>();
            HttpClient client = server.CreateClient();
            return new HostedAuthApp(app, client);
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
    }
}
