using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Integration tests that spin up an in-process <see cref="TestServer"/> hosting the real endpoint
    /// extensions (<see cref="AuthApplicationBuilderExtensions.AddAuthEndpoints"/>) and verify that a
    /// registered <see cref="IAuthAuditLogger"/> receives exactly one hook invocation per endpoint hit,
    /// on both the success and failure paths, with the right structured data.
    /// </summary>
    [TestClass]
    public class AuthAuditLoggerEndpointTests
    {
        private const string Secret = "super_secret_key_12345_12345_12345";

        [TestMethod]
        public async Task LoginEndpoint_OnSuccess_ShouldInvokeOnLoginAsyncWithSucceededResult()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedApp host = await HostedApp.StartAsync(auditLogger, new StubValidationService(succeed: true));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/login", new LoginAuthRequest("alice", "correct-horse"));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, auditLogger.LoginCalls.Count);
            Assert.IsTrue(auditLogger.LoginCalls[0].Result.Success);
            Assert.AreEqual("alice", auditLogger.LoginCalls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task LoginEndpoint_OnFailure_ShouldInvokeOnLoginAsyncWithFailedResultAndAttemptedSubject()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedApp host = await HostedApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/login", new LoginAuthRequest("mallory", "wrong"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(1, auditLogger.LoginCalls.Count);
            Assert.IsFalse(auditLogger.LoginCalls[0].Result.Success);
            Assert.AreEqual("mallory", auditLogger.LoginCalls[0].Result.AttemptedSubject);
            Assert.AreEqual(LoginFailureReason.InvalidCredentials, auditLogger.LoginCalls[0].Result.FailureReason);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_OnSuccess_ShouldInvokeOnApiKeyAuthAsync()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedApp host = await HostedApp.StartAsync(auditLogger, new StubValidationService(succeed: true));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/apikey", new ApiKeyAuthRequest("valid-key"));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, auditLogger.ApiKeyCalls.Count);
            Assert.IsTrue(auditLogger.ApiKeyCalls[0].Result.Success);
        }

        [TestMethod]
        public async Task ApiKeyEndpoint_OnFailure_ShouldInvokeOnApiKeyAuthAsyncWithFailedResult()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedApp host = await HostedApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/apikey", new ApiKeyAuthRequest("bad-key"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(1, auditLogger.ApiKeyCalls.Count);
            Assert.IsFalse(auditLogger.ApiKeyCalls[0].Result.Success);
            Assert.AreEqual(ApiKeyAuthFailureReason.UnknownKey, auditLogger.ApiKeyCalls[0].Result.FailureReason);
        }

        [TestMethod]
        public async Task RefreshEndpoint_WithUnknownToken_ShouldInvokeOnRefreshAsyncWithTokenNotFound()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedApp host = await HostedApp.StartAsync(auditLogger, new StubValidationService(succeed: true));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/refresh", new RefreshRequest("unknown-refresh-token"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(1, auditLogger.RefreshCalls.Count);
            Assert.IsFalse(auditLogger.RefreshCalls[0].Result.Success);
            Assert.AreEqual(RefreshFailureReason.TokenNotFound, auditLogger.RefreshCalls[0].Result.FailureReason);
        }

        [TestMethod]
        public async Task LogoutEndpoint_WithUnknownToken_ShouldInvokeOnLogoutAsyncWithUnknownResult()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedApp host = await HostedApp.StartAsync(auditLogger, new StubValidationService(succeed: true));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/logout", new LogoutRequest("unknown-token"));

            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            Assert.AreEqual(1, auditLogger.LogoutCalls.Count);
            Assert.IsFalse(auditLogger.LogoutCalls[0].Result.WasKnown);
            Assert.IsNotNull(auditLogger.LogoutCalls[0].HttpContext);
        }

        [TestMethod]
        public async Task LogoutEndpoint_WithKnownToken_ShouldInvokeOnLogoutAsyncWithKnownResult()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            FakeRefreshTokenStore store = new FakeRefreshTokenStore();
            await using HostedApp host = await HostedApp.StartAsync(auditLogger, new StubValidationService(succeed: true), store);

            // Issue a refresh token via the service layer so we know a valid token exists.
            IRefreshTokenService refreshService = host.App.Services.GetRequiredService<IRefreshTokenService>();
            string rawToken = await refreshService.CreateRefreshTokenAsync("user-42", "user", null, null);

            // CreateRefreshTokenAsync does not invoke the audit logger — clear the LogoutCalls for cleanliness
            // (not strictly needed because CreateRefreshTokenAsync has no hook, but being explicit).
            auditLogger.LogoutCalls.Clear();

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/logout", new LogoutRequest(rawToken));

            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            Assert.AreEqual(1, auditLogger.LogoutCalls.Count);
            Assert.IsTrue(auditLogger.LogoutCalls[0].Result.WasKnown);
            Assert.AreEqual("user-42", auditLogger.LogoutCalls[0].Result.Subject);
            Assert.IsNotNull(auditLogger.LogoutCalls[0].HttpContext);
        }

        [TestMethod]
        public async Task LogoutEndpoint_WithoutRegisteredAuditLogger_ShouldStillReturn204()
        {
            await using HostedApp host = await HostedApp.StartAsync(auditLogger: null, new StubValidationService(succeed: true));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/logout", new LogoutRequest("anything"));

            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        /// <summary>
        /// Helper that builds a real <see cref="WebApplication"/> with the library's own
        /// <see cref="AuthApplicationBuilderExtensions.AddAuthEndpoints"/> and exposes a
        /// <see cref="TestServer"/>-backed <see cref="HttpClient"/>.
        /// </summary>
        private sealed class HostedApp : IAsyncDisposable
        {
            public WebApplication App { get; }
            public HttpClient Client { get; }

            private HostedApp(WebApplication app, HttpClient client)
            {
                App = app;
                Client = client;
            }

            public static async Task<HostedApp> StartAsync(
                RecordingAuditLogger? auditLogger,
                IAuthRequestValidationService validationService,
                FakeRefreshTokenStore? store = null)
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder();
                builder.WebHost.UseTestServer();
                builder.Logging.ClearProviders();

                builder.Services.AddEasyReasyAuth(Secret);
                builder.Services.AddSingleton<IAuthRequestValidationService>(validationService);

                FakeRefreshTokenStore sharedStore = store ?? new FakeRefreshTokenStore();
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
                return new HostedApp(app, client);
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

        private sealed class StubValidationService : IAuthRequestValidationService
        {
            private readonly bool _succeed;

            public StubValidationService(bool succeed)
            {
                _succeed = succeed;
            }

            public Task<ApiKeyAuthResult> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
            {
                if (_succeed)
                {
                    DateTime expiresAt = DateTime.UtcNow.AddHours(1);
                    string token = jwtTokenService.CreateToken("test-client", "apikey", new List<Claim>(), new List<string>(), expiresAt);
                    return Task.FromResult(ApiKeyAuthResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), "test-client"));
                }
                return Task.FromResult(ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey, attemptedClientId: request.ApiKey));
            }

            public Task<LoginResult> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
            {
                if (_succeed)
                {
                    DateTime expiresAt = DateTime.UtcNow.AddHours(1);
                    string token = jwtTokenService.CreateToken(request.Username, "user", new List<Claim>(), new List<string>(), expiresAt);
                    return Task.FromResult(LoginResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), request.Username));
                }
                return Task.FromResult(LoginResult.Failed(LoginFailureReason.InvalidCredentials, attemptedSubject: request.Username));
            }
        }
    }
}
