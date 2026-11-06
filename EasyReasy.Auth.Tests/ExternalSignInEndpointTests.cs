using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// <see cref="ExternalSignInEndpoint.HandleAsync"/> on a real <see cref="WebApplication"/>: the response, the
    /// audit record, the access-token cookie and the progressive-delay mark, for a sign-in that succeeds and one that
    /// is refused.
    /// </summary>
    [TestClass]
    public class ExternalSignInEndpointTests
    {
        private const string Secret = "super_secret_key_12345_12345_12345";
        private const string Path = "/api/auth/provider";

        [TestMethod]
        public async Task HandleAsync_SignInSucceeds_Returns200WithTheResponseAuditsAndMarksTheRequest()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            AuthResponse authResponse = new AuthResponse(CreateToken(), "2099-01-01T00:00:00Z");
            await using Host host = await Host.StartAsync(ExternalAuthResult.Succeeded("provider", authResponse, "subject-1"), auditLogger);

            HttpResponseMessage response = await host.Client.PostAsync(Path, null);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.AreEqual(authResponse.Token, (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token);
            Assert.AreEqual("subject-1", auditLogger.ExternalAuthCalls.Single().Result.AttemptedSubject);
            Assert.IsTrue(host.Marks.Single());
        }

        [TestMethod]
        public async Task HandleAsync_SignInRefused_Returns401AuditsAndLeavesTheRequestUnmarked()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            ExternalAuthResult refusal = ExternalAuthResult.Failed("provider", ExternalAuthFailureReason.Rejected, "subject-1", "NotAllowed");
            await using Host host = await Host.StartAsync(refusal, auditLogger);

            HttpResponseMessage response = await host.Client.PostAsync(Path, null);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.AreSame(refusal, auditLogger.ExternalAuthCalls.Single().Result);
            Assert.IsFalse(host.Marks.Single());
        }

        [TestMethod]
        public async Task HandleAsync_ServicesRegistered_HandsTheProviderTheJwtAndRefreshTokenServices()
        {
            InertRefreshTokenService refreshTokenService = new InertRefreshTokenService();
            await using Host host = await Host.StartAsync(ExternalAuthResult.Failed("provider", ExternalAuthFailureReason.InvalidToken), auditLogger: null, refreshTokenService: refreshTokenService);

            await host.Client.PostAsync(Path, null);

            Assert.AreSame(host.App.Services.GetRequiredService<IJwtTokenService>(), host.ReceivedJwtTokenService);
            Assert.AreSame(refreshTokenService, host.ReceivedRefreshTokenService);
        }

        [TestMethod]
        public async Task HandleAsync_NoRefreshTokenServiceOrAuditLogger_HandsTheProviderNullAndStillAnswers()
        {
            await using Host host = await Host.StartAsync(ExternalAuthResult.Failed("provider", ExternalAuthFailureReason.InvalidToken), auditLogger: null);

            HttpResponseMessage response = await host.Client.PostAsync(Path, null);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.IsNull(host.ReceivedRefreshTokenService);
        }

        [TestMethod]
        public async Task HandleAsync_CookieOptionOnAndSignInSucceeds_SetsTheAccessTokenCookie()
        {
            string token = CreateToken();
            await using Host host = await Host.StartAsync(
                ExternalAuthResult.Succeeded("provider", new AuthResponse(token, "unused"), "subject-1"), auditLogger: null, cookieTokenPaths: ["/files"]);

            HttpResponseMessage response = await host.Client.PostAsync(Path, null);

            StringAssert.StartsWith(SetCookieHeaders(response).Single(), $"__Secure-access_token={token}; max-age=");
        }

        [TestMethod]
        public async Task HandleAsync_CookieOptionOnAndSignInRefused_SetsNoCookie()
        {
            await using Host host = await Host.StartAsync(
                ExternalAuthResult.Failed("provider", ExternalAuthFailureReason.Rejected, "subject-1"), auditLogger: null, cookieTokenPaths: ["/files"]);

            HttpResponseMessage response = await host.Client.PostAsync(Path, null);

            Assert.AreEqual(0, SetCookieHeaders(response).Length);
        }

        [TestMethod]
        public async Task HandleAsync_CookieOptionOffAndSignInSucceeds_SetsNoCookie()
        {
            await using Host host = await Host.StartAsync(
                ExternalAuthResult.Succeeded("provider", new AuthResponse(CreateToken(), "unused"), "subject-1"), auditLogger: null);

            HttpResponseMessage response = await host.Client.PostAsync(Path, null);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(0, SetCookieHeaders(response).Length);
        }

        [TestMethod]
        public async Task HandleAsync_AuditLoggerThrows_Propagates()
        {
            await using Host host = await Host.StartAsync(ExternalAuthResult.Failed("provider", ExternalAuthFailureReason.InvalidToken), new ThrowingExternalAuthLogger());

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => host.Client.PostAsync(Path, null));
        }

        [TestMethod]
        public async Task HandleAsync_NullArgument_ThrowsArgumentNullException()
        {
            HttpContext? missingContext = null;
            Func<IJwtTokenService, IRefreshTokenService?, Task<ExternalAuthResult>>? missingAuthenticate = null;

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => ExternalSignInEndpoint.HandleAsync(
                missingContext!, (jwt, refresh) => Task.FromResult(ExternalAuthResult.Failed("provider", ExternalAuthFailureReason.InvalidToken))));
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => ExternalSignInEndpoint.HandleAsync(new DefaultHttpContext(), missingAuthenticate!));
        }

        private static string CreateToken()
        {
            return new JwtTokenService(Secret).CreateToken("subject-1", "user", [], [], DateTime.UtcNow.AddMinutes(5));
        }

        private static string[] SetCookieHeaders(HttpResponseMessage response)
        {
            return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values) ? values.ToArray() : [];
        }

        /// <summary>
        /// A <see cref="WebApplication"/> whose one endpoint is <see cref="ExternalSignInEndpoint.HandleAsync"/> with a
        /// provider that returns preset results and records the services it was handed.
        /// </summary>
        private sealed class Host : IAsyncDisposable
        {
            private Host(WebApplication app, HttpClient client)
            {
                App = app;
                Client = client;
            }

            public WebApplication App { get; }

            public HttpClient Client { get; }

            public List<bool> Marks { get; } = new List<bool>();

            public IJwtTokenService? ReceivedJwtTokenService { get; private set; }

            public IRefreshTokenService? ReceivedRefreshTokenService { get; private set; }

            public static async Task<Host> StartAsync(
                ExternalAuthResult result,
                IAuthAuditLogger? auditLogger,
                IRefreshTokenService? refreshTokenService = null,
                IReadOnlyList<string>? cookieTokenPaths = null)
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder();
                builder.WebHost.UseTestServer();
                builder.Logging.ClearProviders();
                builder.Services.AddEasyReasyAuth(Secret, options => options.CookieTokenPaths = cookieTokenPaths ?? []);

                if (auditLogger != null)
                {
                    builder.Services.AddSingleton(auditLogger);
                }

                if (refreshTokenService != null)
                {
                    builder.Services.AddSingleton(refreshTokenService);
                }

                WebApplication app = builder.Build();
                Host? host = null;

                // Reads back the mark ProgressiveDelayMiddleware acts on, after the endpoint has run; that the
                // middleware clears a marked request's failure count is ProgressiveDelayMiddlewareTests' concern.
                app.Use(async (context, next) =>
                {
                    await next();
                    host!.Marks.Add(context.HasAuthenticationSucceeded());
                });

                // Typed as a Func, not a lambda on HttpContext alone, which would bind as a RequestDelegate and drop
                // the IResult; a provider's endpoint takes its request body too, so it never hits this.
                Func<HttpContext, Task<IResult>> endpoint = httpContext => ExternalSignInEndpoint.HandleAsync(httpContext, (jwtTokenService, refreshService) =>
                {
                    host!.ReceivedJwtTokenService = jwtTokenService;
                    host.ReceivedRefreshTokenService = refreshService;
                    return Task.FromResult(result);
                });
                app.MapPost(Path, endpoint);

                await app.StartAsync();
                TestServer server = (TestServer)app.Services.GetRequiredService<IServer>();
                host = new Host(app, server.CreateClient());
                return host;
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

        private sealed class ThrowingExternalAuthLogger : IAuthAuditLogger
        {
            public Task OnExternalAuthAsync(HttpContext httpContext, ExternalAuthResult result)
            {
                throw new InvalidOperationException("audit logger failure");
            }
        }

        /// <summary>
        /// An <see cref="IRefreshTokenService"/> only handed to the provider; nothing on this path calls it.
        /// </summary>
        private sealed class InertRefreshTokenService : IRefreshTokenService
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
