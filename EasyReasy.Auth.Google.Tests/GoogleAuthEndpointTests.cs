using EasyReasy.Auth;
using EasyReasy.Auth.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

namespace EasyReasy.Auth.Google.Tests
{
    /// <summary>
    /// Integration tests that spin up an in-process <see cref="TestServer"/> hosting the real
    /// <see cref="GoogleAuthApplicationBuilderExtensions.AddGoogleAuthEndpoint"/> and verify that a registered
    /// <see cref="IAuthAuditLogger"/> receives exactly one <see cref="IAuthAuditLogger.OnExternalAuthAsync"/>
    /// invocation per endpoint hit (on both the success and failure paths), that the endpoint sets the
    /// <c>Cache-Control: no-store</c> header, that it works when no audit logger is registered, and that it
    /// fails fast when its required services are missing.
    /// </summary>
    [TestClass]
    public class GoogleAuthEndpointTests
    {
        private const string Secret = "super_secret_key_12345_12345_12345";

        [TestMethod]
        public async Task GoogleEndpoint_ValidToken_ReturnsOkAndInvokesAuditHook()
        {
            RecordingExternalAuthLogger auditLogger = new RecordingExternalAuthLogger();
            FakeGoogleIdTokenValidator validator = new FakeGoogleIdTokenValidator
            {
                UserInfoToReturn = new GoogleUserInfo { Subject = "sub-123", Email = "user@example.com", EmailVerified = true },
            };
            FakeGoogleAuthHandler handler = new FakeGoogleAuthHandler
            {
                ResponseToReturn = new AuthResponse("jwt-token", "2099-01-01T00:00:00Z"),
            };
            await using HostedApp host = await HostedApp.StartAsync(validator, handler, auditLogger);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/google", new { idToken = "valid-token" });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.AreEqual(1, auditLogger.Calls.Count);
            Assert.IsTrue(auditLogger.Calls[0].Result.Success);
            Assert.AreEqual("google", auditLogger.Calls[0].Result.Provider);
            Assert.AreEqual("sub-123", auditLogger.Calls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task GoogleEndpoint_InvalidToken_ReturnsUnauthorizedAndInvokesAuditHook()
        {
            RecordingExternalAuthLogger auditLogger = new RecordingExternalAuthLogger();
            FakeGoogleIdTokenValidator validator = new FakeGoogleIdTokenValidator
            {
                ExceptionToThrow = new GoogleTokenValidationException("Invalid token"),
            };
            await using HostedApp host = await HostedApp.StartAsync(validator, new FakeGoogleAuthHandler(), auditLogger);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/google", new { idToken = "bad-token" });

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.AreEqual(1, auditLogger.Calls.Count);
            Assert.IsFalse(auditLogger.Calls[0].Result.Success);
            Assert.AreEqual(ExternalAuthFailureReason.InvalidToken, auditLogger.Calls[0].Result.FailureReason);
            Assert.IsNull(auditLogger.Calls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task GoogleEndpoint_HandlerRejection_ReturnsUnauthorizedWithRejectedReason()
        {
            RecordingExternalAuthLogger auditLogger = new RecordingExternalAuthLogger();
            FakeGoogleIdTokenValidator validator = new FakeGoogleIdTokenValidator
            {
                UserInfoToReturn = new GoogleUserInfo { Subject = "sub-456", Email = "rejected@example.com", EmailVerified = true },
            };
            FakeGoogleAuthHandler handler = new FakeGoogleAuthHandler { ResponseToReturn = null };
            await using HostedApp host = await HostedApp.StartAsync(validator, handler, auditLogger);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/google", new { idToken = "valid-token" });

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(1, auditLogger.Calls.Count);
            Assert.IsFalse(auditLogger.Calls[0].Result.Success);
            Assert.AreEqual(ExternalAuthFailureReason.Rejected, auditLogger.Calls[0].Result.FailureReason);
            Assert.AreEqual("sub-456", auditLogger.Calls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task GoogleEndpoint_NoAuditLogger_ReturnsOk()
        {
            FakeGoogleIdTokenValidator validator = new FakeGoogleIdTokenValidator
            {
                UserInfoToReturn = new GoogleUserInfo { Subject = "sub-789", Email = "user@example.com", EmailVerified = true },
            };
            FakeGoogleAuthHandler handler = new FakeGoogleAuthHandler
            {
                ResponseToReturn = new AuthResponse("jwt-token", "2099-01-01T00:00:00Z"),
            };
            await using HostedApp host = await HostedApp.StartAsync(validator, handler, auditLogger: null);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/google", new { idToken = "valid-token" });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task GoogleEndpoint_RefreshTokenServiceRegistered_PassesItThroughToHandler()
        {
            FakeGoogleIdTokenValidator validator = new FakeGoogleIdTokenValidator
            {
                UserInfoToReturn = new GoogleUserInfo { Subject = "sub-refresh", Email = "user@example.com", EmailVerified = true },
            };
            FakeGoogleAuthHandler handler = new FakeGoogleAuthHandler
            {
                ResponseToReturn = new AuthResponse("jwt-token", "2099-01-01T00:00:00Z"),
            };
            FakeRefreshTokenService refreshTokenService = new FakeRefreshTokenService();
            await using HostedApp host = await HostedApp.StartAsync(validator, handler, auditLogger: null, refreshTokenService);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/google", new { idToken = "valid-token" });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreSame(refreshTokenService, handler.LastReceivedRefreshTokenService);
        }

        [TestMethod]
        public async Task GoogleEndpoint_WithoutRegisteredHandler_ThrowsAtStartup()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddEasyReasyAuth(Secret);
            builder.Services.AddEasyReasyGoogleAuth("client-id.apps.googleusercontent.com");
            // Deliberately not registering IGoogleAuthHandler.

            await using WebApplication app = builder.Build();

            Assert.ThrowsException<InvalidOperationException>(() => app.AddGoogleAuthEndpoint());
        }

        [TestMethod]
        public async Task GoogleEndpoint_WithoutRegisteredValidator_ThrowsAtStartup()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddEasyReasyAuth(Secret);
            // Deliberately not calling AddEasyReasyGoogleAuth, so IGoogleIdTokenValidator is absent.

            await using WebApplication app = builder.Build();

            Assert.ThrowsException<InvalidOperationException>(() => app.AddGoogleAuthEndpoint());
        }

        [TestMethod]
        public async Task GoogleEndpoint_WithoutRegisteredJwtTokenService_ThrowsAtStartup()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddEasyReasyGoogleAuth("client-id.apps.googleusercontent.com");
            builder.Services.AddScoped<IGoogleAuthHandler, FakeGoogleAuthHandler>();
            // Deliberately not calling AddEasyReasyAuth, so IJwtTokenService is absent.

            await using WebApplication app = builder.Build();

            Assert.ThrowsException<InvalidOperationException>(() => app.AddGoogleAuthEndpoint());
        }

        [TestMethod]
        public async Task GoogleEndpoint_AuditLoggerThrows_PropagatesOutOfEndpoint()
        {
            FakeGoogleIdTokenValidator validator = new FakeGoogleIdTokenValidator
            {
                UserInfoToReturn = new GoogleUserInfo { Subject = "sub-1", Email = "user@example.com", EmailVerified = true },
            };
            FakeGoogleAuthHandler handler = new FakeGoogleAuthHandler
            {
                ResponseToReturn = new AuthResponse("jwt-token", "2099-01-01T00:00:00Z"),
            };
            await using HostedApp host = await HostedApp.StartAsync(validator, handler, new ThrowingExternalAuthLogger());

            // The endpoint does not swallow audit-logger failures. Under TestServer the unhandled exception
            // surfaces directly on the call; in a hosted server it would become a 500.
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" }));
        }

        /// <summary>
        /// Builds a real <see cref="WebApplication"/> with <see cref="GoogleAuthApplicationBuilderExtensions.AddGoogleAuthEndpoint"/>
        /// wired to test fakes, and exposes a <see cref="TestServer"/>-backed <see cref="HttpClient"/>.
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
                IGoogleIdTokenValidator validator,
                IGoogleAuthHandler handler,
                IAuthAuditLogger? auditLogger,
                IRefreshTokenService? refreshTokenService = null)
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder();
                builder.WebHost.UseTestServer();
                builder.Logging.ClearProviders();

                builder.Services.AddEasyReasyAuth(Secret);
                builder.Services.AddSingleton<IGoogleIdTokenValidator>(validator);
                builder.Services.AddSingleton<IGoogleAuthHandler>(handler);
                builder.Services.AddScoped<GoogleAuthService>();

                if (auditLogger != null)
                {
                    builder.Services.AddSingleton<IAuthAuditLogger>(auditLogger);
                }

                if (refreshTokenService != null)
                {
                    builder.Services.AddSingleton<IRefreshTokenService>(refreshTokenService);
                }

                WebApplication app = builder.Build();
                app.AddGoogleAuthEndpoint();

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

        /// <summary>
        /// In-memory <see cref="IAuthAuditLogger"/> that records every <see cref="OnExternalAuthAsync"/> invocation.
        /// All other hooks use the interface's default no-op implementations.
        /// </summary>
        private sealed class RecordingExternalAuthLogger : IAuthAuditLogger
        {
            public List<(HttpContext HttpContext, ExternalAuthResult Result)> Calls { get; } = new List<(HttpContext, ExternalAuthResult)>();

            public Task OnExternalAuthAsync(HttpContext httpContext, ExternalAuthResult result)
            {
                Calls.Add((httpContext, result));
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// <see cref="IAuthAuditLogger"/> whose <see cref="OnExternalAuthAsync"/> always throws, used to verify
        /// that an audit-logger failure propagates out of the endpoint rather than being swallowed.
        /// </summary>
        private sealed class ThrowingExternalAuthLogger : IAuthAuditLogger
        {
            public Task OnExternalAuthAsync(HttpContext httpContext, ExternalAuthResult result)
            {
                throw new InvalidOperationException("audit logger failure");
            }
        }

        /// <summary>
        /// Inert <see cref="IRefreshTokenService"/> used only to assert that the endpoint resolves a
        /// registered refresh-token service and passes the same instance through to the handler. None of
        /// its members are exercised by that path, so they throw rather than return fabricated results.
        /// </summary>
        private sealed class FakeRefreshTokenService : IRefreshTokenService
        {
            public Task<string> CreateRefreshTokenAsync(string subject, string authType, string? serializedClaims, string? serializedRoles, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<RefreshTokenCreationResult> CreateRefreshTokenWithFamilyAsync(string subject, string authType, string? serializedClaims, string? serializedRoles, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<LogoutResult> LogoutAsync(string? refreshToken, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<SessionRevocationResult> InvalidateAllSessionsAsync(string subject, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task RetireFamilyAsync(string? familyId, string? subject = null, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }
    }
}
