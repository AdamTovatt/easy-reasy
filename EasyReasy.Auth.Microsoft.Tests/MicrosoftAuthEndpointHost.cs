using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// A real <see cref="WebApplication"/> serving <see cref="MicrosoftAuthApplicationBuilderExtensions.AddMicrosoftAuthEndpoints"/>
    /// on a <see cref="TestServer"/>, wired to a <see cref="FakeMicrosoftIdentityProvider"/>, a fake clock and a
    /// <see cref="FakeMicrosoftAuthHandler"/>, with helpers that carry a <see cref="FakeBrowser"/>'s cookies.
    /// </summary>
    internal sealed class MicrosoftAuthEndpointHost : IAsyncDisposable
    {
        public const string JwtSecret = "super_secret_key_12345_12345_12345";

        private MicrosoftAuthEndpointHost(WebApplication app, HttpClient client, FakeTimeProvider clock, FakeMicrosoftIdentityProvider identityProvider, FakeMicrosoftAuthHandler handler)
        {
            App = app;
            Client = client;
            Clock = clock;
            IdentityProvider = identityProvider;
            Handler = handler;
        }

        public WebApplication App { get; }

        public HttpClient Client { get; }

        public FakeTimeProvider Clock { get; }

        public FakeMicrosoftIdentityProvider IdentityProvider { get; }

        public FakeMicrosoftAuthHandler Handler { get; }

        public static async Task<MicrosoftAuthEndpointHost> StartAsync(
            IAuthAuditLogger? auditLogger = null,
            IRefreshTokenService? refreshTokenService = null,
            IReadOnlyList<string>? cookieTokenPaths = null,
            MicrosoftAuthOptions? options = null)
        {
            FakeTimeProvider clock = new FakeTimeProvider(MicrosoftAuthHarness.StartTime);
            FakeMicrosoftIdentityProvider identityProvider = new FakeMicrosoftIdentityProvider(clock);
            FakeMicrosoftAuthHandler handler = new FakeMicrosoftAuthHandler
            {
                ResponseToReturn = new AuthResponse(CreateAccessToken(), "2030-01-01T09:05:00Z"),
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddEasyReasyAuth(JwtSecret, options => options.CookieTokenPaths = cookieTokenPaths ?? []);
            builder.Services.AddEasyReasyMicrosoftAuth(options ?? MicrosoftAuthHarness.CreateOptions());
            MicrosoftAuthHarness.ConfigureFakeIdentityProvider(builder.Services, identityProvider);
            builder.Services.AddSingleton<IMicrosoftAuthHandler>(handler);

            if (auditLogger != null)
            {
                builder.Services.AddSingleton(auditLogger);
            }

            if (refreshTokenService != null)
            {
                builder.Services.AddSingleton(refreshTokenService);
            }

            WebApplication app = builder.Build();
            app.AddMicrosoftAuthEndpoints();
            await app.StartAsync();

            TestServer server = (TestServer)app.Services.GetRequiredService<IServer>();
            return new MicrosoftAuthEndpointHost(app, server.CreateClient(), clock, identityProvider, handler);
        }

        /// <summary>
        /// An access token that expires five minutes after the fake clock's start, so the access-token cookie it is
        /// written into has a positive max age on that clock.
        /// </summary>
        public static string CreateAccessToken()
        {
            return new JwtTokenService(JwtSecret).CreateToken("user-7", "microsoft", [], [], MicrosoftAuthHarness.StartTime.UtcDateTime.AddMinutes(5));
        }

        public FakeBrowser CreateBrowser()
        {
            return new FakeBrowser(Clock);
        }

        /// <summary>
        /// Posts to the begin endpoint as the browser, with the given JSON body (or none).
        /// </summary>
        public async Task<HttpResponseMessage> PostBeginAsync(FakeBrowser browser, object? body = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/microsoft/begin");
            if (body != null)
            {
                request.Content = JsonContent.Create(body);
            }

            return await SendAsync(request, browser);
        }

        /// <summary>
        /// Begins a sign-in as the browser and returns the authorize URL it is sent to.
        /// </summary>
        public async Task<string> BeginAsync(FakeBrowser browser, object? body = null)
        {
            HttpResponseMessage response = await PostBeginAsync(browser, body);
            response.EnsureSuccessStatusCode();
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("authorizationUrl").GetString()!;
        }

        /// <summary>
        /// Posts a code and state to the sign-in endpoint as the browser.
        /// </summary>
        public async Task<HttpResponseMessage> PostSignInAsync(FakeBrowser browser, string code, string state)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/microsoft")
            {
                Content = JsonContent.Create(new { code, state }),
            };

            return await SendAsync(request, browser);
        }

        /// <summary>
        /// Runs a whole sign-in in the browser: begin, the user signing in at the fake identity provider, and the
        /// sign-in post.
        /// </summary>
        public async Task<HttpResponseMessage> SignInAsync(FakeBrowser browser, Action<FakeIdTokenSettings>? customizeToken = null)
        {
            string authorizationUrl = await BeginAsync(browser);
            string code = IdentityProvider.IssueCode(authorizationUrl, customizeToken);
            return await PostSignInAsync(browser, code, ReadState(authorizationUrl));
        }

        public static string ReadState(string authorizationUrl)
        {
            return QueryHelpers.ParseQuery(new Uri(authorizationUrl).Query)["state"].ToString();
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

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, FakeBrowser browser)
        {
            browser.ApplyTo(request);
            HttpResponseMessage response = await Client.SendAsync(request);
            browser.Absorb(response);
            return response;
        }
    }
}
