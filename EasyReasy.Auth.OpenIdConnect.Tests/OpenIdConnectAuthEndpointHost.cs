using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// A real <see cref="WebApplication"/> serving <see cref="OpenIdConnectAuthApplicationBuilderExtensions.AddOpenIdConnectAuthEndpoints"/>
    /// on a <see cref="TestServer"/>, wired to the fake issuers, a fake clock, a <see cref="FakeProviderResolver"/> and a
    /// <see cref="FakeOpenIdConnectAuthHandler"/>, with helpers that carry a <see cref="FakeBrowser"/>'s cookies.
    /// </summary>
    internal sealed class OpenIdConnectAuthEndpointHost : IAsyncDisposable
    {
        public const string JwtSecret = "super_secret_key_12345_12345_12345";

        private OpenIdConnectAuthEndpointHost(
            WebApplication app,
            HttpClient client,
            FakeTimeProvider clock,
            FakeOpenIdConnectIssuer primary,
            FakeProviderResolver resolver,
            FakeOpenIdConnectAuthHandler handler)
        {
            App = app;
            Client = client;
            Clock = clock;
            Primary = primary;
            Resolver = resolver;
            Handler = handler;
        }

        public WebApplication App { get; }

        public HttpClient Client { get; }

        public FakeTimeProvider Clock { get; }

        public FakeOpenIdConnectIssuer Primary { get; }

        public FakeProviderResolver Resolver { get; }

        public FakeOpenIdConnectAuthHandler Handler { get; }

        public static async Task<OpenIdConnectAuthEndpointHost> StartAsync(
            IAuthAuditLogger? auditLogger = null,
            IRefreshTokenService? refreshTokenService = null,
            IReadOnlyList<string>? cookieTokenPaths = null,
            Action<OpenIdConnectAuthEndpoints>? configureEndpoints = null)
        {
            FakeTimeProvider clock = new FakeTimeProvider(OpenIdConnectAuthHarness.StartTime);
            FakeNetwork network = new FakeNetwork();
            FakeOpenIdConnectIssuer primary = new FakeOpenIdConnectIssuer(clock, "primary.fake.test");
            network.Add(primary);
            FakeProviderResolver resolver = new FakeProviderResolver();
            resolver.Providers[OpenIdConnectAuthHarness.PrimaryKey] = OpenIdConnectAuthHarness.CreateProvider(primary, OpenIdConnectAuthHarness.PrimaryKey);
            FakeOpenIdConnectAuthHandler handler = new FakeOpenIdConnectAuthHandler
            {
                ResponseToReturn = new AuthResponse(CreateAccessToken(), "2030-01-01T09:05:00Z"),
            };

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddEasyReasyAuth(JwtSecret, options => options.CookieTokenPaths = cookieTokenPaths ?? []);
            builder.Services.AddEasyReasyOpenIdConnectAuth(OpenIdConnectAuthHarness.CreateOptions());
            OpenIdConnectAuthHarness.ConfigureNetwork(builder.Services, network);
            builder.Services.AddSingleton<IOpenIdConnectProviderResolver>(resolver);
            builder.Services.AddSingleton<IOpenIdConnectAuthHandler>(handler);

            if (auditLogger != null)
            {
                builder.Services.AddSingleton(auditLogger);
            }

            if (refreshTokenService != null)
            {
                builder.Services.AddSingleton(refreshTokenService);
            }

            WebApplication app = builder.Build();
            OpenIdConnectAuthEndpoints endpoints = app.AddOpenIdConnectAuthEndpoints();
            configureEndpoints?.Invoke(endpoints);
            await app.StartAsync();

            TestServer server = (TestServer)app.Services.GetRequiredService<IServer>();
            return new OpenIdConnectAuthEndpointHost(app, server.CreateClient(), clock, primary, resolver, handler);
        }

        /// <summary>
        /// An access token that expires five minutes after the fake clock's start, so the access-token cookie it is
        /// written into has a positive max age on that clock.
        /// </summary>
        public static string CreateAccessToken()
        {
            return new JwtTokenService(JwtSecret).CreateToken("user-7", "oidc", [], [], OpenIdConnectAuthHarness.StartTime.UtcDateTime.AddMinutes(5));
        }

        public FakeBrowser CreateBrowser()
        {
            return new FakeBrowser(Clock);
        }

        /// <summary>
        /// Posts to the begin endpoint as the browser, with the given JSON body.
        /// </summary>
        public async Task<HttpResponseMessage> PostBeginAsync(FakeBrowser browser, object body)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc/begin")
            {
                Content = JsonContent.Create(body),
            };

            return await SendAsync(request, browser);
        }

        /// <summary>
        /// Begins a sign-in with the primary provider as the browser and returns the authorize URL it is sent to.
        /// </summary>
        public async Task<string> BeginAsync(FakeBrowser browser, string? loginHint = null)
        {
            HttpResponseMessage response = await PostBeginAsync(browser, new { providerKey = OpenIdConnectAuthHarness.PrimaryKey, loginHint });
            response.EnsureSuccessStatusCode();
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("authorizationUrl").GetString()!;
        }

        /// <summary>
        /// Posts what the redirect brought back to the sign-in endpoint as the browser, from the primary provider's
        /// sign-in page unless another redirect URI is named.
        /// </summary>
        public async Task<HttpResponseMessage> PostSignInAsync(FakeBrowser browser, string code, string state, string? redirectUri = null, string? iss = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/oidc")
            {
                Content = JsonContent.Create(new
                {
                    code,
                    state,
                    iss,
                    redirectUri = redirectUri ?? OpenIdConnectAuthHarness.SignInRedirectUri(OpenIdConnectAuthHarness.PrimaryKey),
                }),
            };

            return await SendAsync(request, browser);
        }

        /// <summary>
        /// Runs a whole sign-in in the browser: begin, the person signing in at the primary provider, and the sign-in post.
        /// </summary>
        public async Task<HttpResponseMessage> SignInAsync(FakeBrowser browser, Action<FakeIdTokenSettings>? customizeToken = null)
        {
            string authorizationUrl = await BeginAsync(browser);
            string code = Primary.IssueCode(authorizationUrl, customizeToken);
            return await PostSignInAsync(browser, code, ReadState(authorizationUrl));
        }

        public static string ReadState(string authorizationUrl)
        {
            return OpenIdConnectAuthHarness.ParseQuery(authorizationUrl)["state"].ToString();
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
