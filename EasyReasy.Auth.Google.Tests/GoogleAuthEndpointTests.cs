using EasyReasy.Auth;
using EasyReasy.Auth.Google;
using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

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
            InertRefreshTokenService refreshTokenService = new InertRefreshTokenService();
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
        public async Task GoogleEndpoint_NothingRegistered_NamesEveryMissingServiceInOneException()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();

            await using WebApplication app = builder.Build();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => app.AddGoogleAuthEndpoint());
            StringAssert.Contains(exception.Message, nameof(IGoogleIdTokenValidator));
            StringAssert.Contains(exception.Message, nameof(IGoogleAuthHandler));
            StringAssert.Contains(exception.Message, nameof(IJwtTokenService));
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

        [TestMethod]
        public async Task GoogleEndpoint_CookieOptionOnAndSignInSucceeds_SetsCookieCarryingTheIssuedToken()
        {
            string token = new JwtTokenService(Secret).CreateToken("sub-cookie", "user", [], [], DateTime.UtcNow.AddMinutes(5));
            await using HostedApp host = await HostedApp.StartAsync(
                AcceptingValidator(), new FakeGoogleAuthHandler { ResponseToReturn = new AuthResponse(token, "unused") },
                auditLogger: null, cookieTokenPaths: ["/files"]);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            string cookie = SetCookieHeaders(response).Single();
            StringAssert.StartsWith(cookie, $"__Secure-access_token={token}; max-age=");
            StringAssert.Contains(cookie, "; path=/files; secure; samesite=strict; httponly");
        }

        [TestMethod]
        public async Task GoogleEndpoint_CookieOptionOnAndHandlerRejects_SetsNoCookie()
        {
            await using HostedApp host = await HostedApp.StartAsync(
                AcceptingValidator(), new FakeGoogleAuthHandler { ResponseToReturn = null },
                auditLogger: null, cookieTokenPaths: ["/files"]);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" });

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(0, SetCookieHeaders(response).Length);
        }

        [TestMethod]
        public async Task GoogleEndpoint_CookieOptionOnAndIdTokenInvalid_SetsNoCookie()
        {
            FakeGoogleIdTokenValidator validator = new FakeGoogleIdTokenValidator
            {
                ExceptionToThrow = new GoogleTokenValidationException("Invalid token"),
            };
            await using HostedApp host = await HostedApp.StartAsync(
                validator, new FakeGoogleAuthHandler(), auditLogger: null, cookieTokenPaths: ["/files"]);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = "bad-token" });

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(0, SetCookieHeaders(response).Length);
        }

        [TestMethod]
        public async Task GoogleEndpoint_CookieOptionOffAndSignInSucceeds_SetsNoCookie()
        {
            // A token without exp would make the helper throw, so this also proves the helper is not called.
            await using HostedApp host = await HostedApp.StartAsync(
                AcceptingValidator(), new FakeGoogleAuthHandler { ResponseToReturn = new AuthResponse("jwt-token", "2099-01-01T00:00:00Z") },
                auditLogger: null);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(0, SetCookieHeaders(response).Length);
        }

        [TestMethod]
        public async Task GoogleEndpoint_TokenTooLargeForCookie_SucceedsClearsCookieAndLogsSizeAndLimit()
        {
            // The handler has already issued the session, so the sign-in survives a token that cannot fit; the
            // clear-and-log path is EasyReasy.Auth's, the same one the built-in login endpoint takes.
            string token = new JwtTokenService(Secret).CreateToken(
                "sub-cookie", "user", [new Claim("padding", new string('x', 5000))], [], DateTime.UtcNow.AddMinutes(5));
            ErrorCountingLoggerProvider logs = new ErrorCountingLoggerProvider();
            await using HostedApp host = await HostedApp.StartAsync(
                AcceptingValidator(), new FakeGoogleAuthHandler { ResponseToReturn = new AuthResponse(token, "unused") },
                auditLogger: null, cookieTokenPaths: ["/files"], loggerProvider: logs);

            HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = "valid-token" });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(
                "__Secure-access_token=; max-age=0; path=/files; secure; samesite=strict; httponly",
                SetCookieHeaders(response).Single());
            string message = logs.ErrorMessages.Single(entry => entry.Category == "EasyReasy.Auth.AccessTokenCookie").Message;
            StringAssert.Contains(message, "4096");
        }

        private static FakeGoogleIdTokenValidator AcceptingValidator()
        {
            return new FakeGoogleIdTokenValidator
            {
                UserInfoToReturn = new GoogleUserInfo { Subject = "sub-cookie", Email = "user@example.com", EmailVerified = true },
            };
        }

        private static string[] SetCookieHeaders(HttpResponseMessage response)
        {
            return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values) ? values.ToArray() : [];
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
                IRefreshTokenService? refreshTokenService = null,
                IReadOnlyList<string>? cookieTokenPaths = null,
                ILoggerProvider? loggerProvider = null)
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder();
                builder.WebHost.UseTestServer();
                builder.Logging.ClearProviders();

                if (loggerProvider != null)
                {
                    builder.Logging.AddProvider(loggerProvider);
                }

                builder.Services.AddEasyReasyAuth(Secret, options => options.CookieTokenPaths = cookieTokenPaths ?? []);
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
        /// <see cref="ILoggerProvider"/> that keeps the category and message of every error logged through it.
        /// </summary>
        /// <remarks>
        /// A copy of the idea behind EasyReasy.Auth.Tests' RecordingLoggerProvider, kept here on purpose: this project
        /// references only the two libraries, and a shared test-support project for one small fake is not worth its
        /// weight.
        /// </remarks>
        private sealed class ErrorCountingLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentQueue<(string Category, string Message)> _errors =
                new ConcurrentQueue<(string Category, string Message)>();

            public IReadOnlyList<(string Category, string Message)> ErrorMessages => _errors.ToArray();

            public ILogger CreateLogger(string categoryName) => new ErrorLogger(categoryName, _errors);

            public void Dispose()
            {
            }

            private sealed class ErrorLogger : ILogger
            {
                private readonly string _category;
                private readonly ConcurrentQueue<(string Category, string Message)> _errors;

                public ErrorLogger(string category, ConcurrentQueue<(string Category, string Message)> errors)
                {
                    _category = category;
                    _errors = errors;
                }

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    if (logLevel == LogLevel.Error)
                    {
                        _errors.Enqueue((_category, formatter(state, exception)));
                    }
                }
            }
        }
    }
}
