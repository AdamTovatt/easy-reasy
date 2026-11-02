using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Tests that the built-in login, refresh and logout endpoints set and clear the access-token cookie when
    /// <see cref="EasyReasyAuthOptions.CookieTokenPaths"/> is non-empty, that the API-key endpoint never sets it,
    /// and that nothing changes when the option is off.
    /// </summary>
    [TestClass]
    public class AccessTokenCookieEndpointTests
    {
        private const string LoginBody = "{\"username\":\"alice\",\"password\":\"secret\"}";
        private const string ApiKeyBody = "{\"apiKey\":\"key-1\"}";

        [TestMethod]
        public async Task Login_Success_SetsCookieCarryingTheIssuedToken()
        {
            await using HostedAuthApp host = await StartAsync(cookieOn: true, loginSucceeds: true);

            using HttpResponseMessage response = await PostJsonAsync(host, "/api/auth/login", LoginBody);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            string token = await ReadTokenAsync(response);
            string cookie = SetCookieHeaders(response).Single();
            StringAssert.StartsWith(cookie, $"__Secure-access_token={token}; max-age=");
            StringAssert.Contains(cookie, "; path=/files; secure; samesite=strict; httponly");
        }

        [TestMethod]
        public async Task Login_Failure_SetsNoCookie()
        {
            await using HostedAuthApp host = await StartAsync(cookieOn: true, loginSucceeds: false);

            using HttpResponseMessage response = await PostJsonAsync(host, "/api/auth/login", LoginBody);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(0, SetCookieHeaders(response).Length);
        }

        [TestMethod]
        public async Task Refresh_Success_SetsCookieCarryingTheNewToken()
        {
            await using HostedAuthApp host = await StartAsync(cookieOn: true, loginSucceeds: true);
            string refreshToken = await CreateRefreshTokenAsync(host);

            using HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            string token = await ReadTokenAsync(response);
            StringAssert.StartsWith(SetCookieHeaders(response).Single(), $"__Secure-access_token={token}; max-age=");
        }

        [TestMethod]
        public async Task Refresh_Failure_ClearsCookie()
        {
            await using HostedAuthApp host = await StartAsync(cookieOn: true, loginSucceeds: true);

            using HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest("unknown-refresh-token"));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertClearsCookie(response);
        }

        [TestMethod]
        public async Task Logout_KnownToken_ClearsCookie()
        {
            await using HostedAuthApp host = await StartAsync(cookieOn: true, loginSucceeds: true);
            string refreshToken = await CreateRefreshTokenAsync(host);

            using HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(refreshToken));

            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            AssertClearsCookie(response);
        }

        [TestMethod]
        public async Task Logout_UnknownToken_ClearsCookie()
        {
            await using HostedAuthApp host = await StartAsync(cookieOn: true, loginSucceeds: true);

            using HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest("unknown-refresh-token"));

            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            AssertClearsCookie(response);
        }

        [TestMethod]
        public async Task ApiKey_Success_SetsNoCookie()
        {
            await using HostedAuthApp host = await StartAsync(cookieOn: true, loginSucceeds: true);

            using HttpResponseMessage response = await PostJsonAsync(host, "/api/auth/apikey", ApiKeyBody);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(0, SetCookieHeaders(response).Length);
        }

        [TestMethod]
        public async Task EveryEndpoint_OptionUnset_SetsAndClearsNoCookie()
        {
            await using HostedAuthApp host = await StartAsync(cookieOn: false, loginSucceeds: true);
            string refreshToken = await CreateRefreshTokenAsync(host);

            using HttpResponseMessage login = await PostJsonAsync(host, "/api/auth/login", LoginBody);
            using HttpResponseMessage refresh = await host.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));
            using HttpResponseMessage failedRefresh = await host.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest("unknown-refresh-token"));
            using HttpResponseMessage logout = await host.Client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest("unknown-refresh-token"));

            Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, refresh.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, failedRefresh.StatusCode);
            Assert.AreEqual(HttpStatusCode.NoContent, logout.StatusCode);

            foreach (HttpResponseMessage response in new[] { login, refresh, failedRefresh, logout })
            {
                Assert.AreEqual(0, SetCookieHeaders(response).Length, response.RequestMessage?.RequestUri?.ToString());
            }
        }

        [TestMethod]
        public async Task Login_TokenTooLargeForCookie_SucceedsClearsCookieAndLogsSizeAndLimit()
        {
            RecordingLoggerProvider logs = new RecordingLoggerProvider();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(
                auditLogger: null,
                new LargeTokenValidationService(),
                configureAuth: options => options.CookieTokenPaths = ["/files"],
                loggerProvider: logs);

            using HttpResponseMessage response = await PostJsonAsync(host, "/api/auth/login", LoginBody);

            // The validation service has already issued the session; the response still hands it over.
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue((await ReadTokenAsync(response)).Length > CookieTokenFallback.MaxCookieBytes);
            AssertClearsCookie(response);
            AssertLoggedOversizedCookie(logs);
        }

        [TestMethod]
        public async Task Refresh_TokenTooLargeForCookie_SucceedsClearsCookieLogsErrorAndSessionSurvives()
        {
            // Claims are re-evaluated on refresh, so a token can outgrow the cookie after a login that fit.
            RecordingLoggerProvider logs = new RecordingLoggerProvider();
            FakeRefreshClaimsResolver growingClaims = new FakeRefreshClaimsResolver(context =>
                RefreshClaimsDecision.Allow([new Claim("padding", new string('x', 5000))], []));
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(
                auditLogger: null,
                new StubValidationService(succeed: true),
                configureAuth: options => options.CookieTokenPaths = ["/files"],
                claimsResolver: growingClaims,
                loggerProvider: logs);
            string refreshToken = await CreateRefreshTokenAsync(host);

            using HttpResponseMessage response = await host.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            AssertClearsCookie(response);
            AssertLoggedOversizedCookie(logs);

            // The old refresh token is used up; the new one in the response must still work, or a 500 here would
            // have ended the session.
            string newRefreshToken = await ReadStringPropertyAsync(response, "refreshToken");
            using HttpResponseMessage next = await host.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(newRefreshToken));
            Assert.AreEqual(HttpStatusCode.OK, next.StatusCode);
        }

        private static void AssertLoggedOversizedCookie(RecordingLoggerProvider logs)
        {
            RecordingLoggerProvider.Entry entry = logs.Entries.Single(logged => logged.Category == "EasyReasy.Auth.AccessTokenCookie");
            Assert.AreEqual(LogLevel.Error, entry.Level);
            StringAssert.Contains(entry.Message, "4096");
        }

        private static Task<HostedAuthApp> StartAsync(bool cookieOn, bool loginSucceeds)
        {
            return HostedAuthApp.StartAsync(
                auditLogger: null,
                new StubValidationService(succeed: loginSucceeds),
                configureAuth: options => options.CookieTokenPaths = cookieOn ? ["/files"] : []);
        }

        private static async Task<string> CreateRefreshTokenAsync(HostedAuthApp host)
        {
            IRefreshTokenService refreshService = host.App.Services.GetRequiredService<IRefreshTokenService>();
            return await refreshService.CreateRefreshTokenAsync("alice", "user", null, null);
        }

        private static async Task<HttpResponseMessage> PostJsonAsync(HostedAuthApp host, string path, string json)
        {
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            return await host.Client.PostAsync(path, content);
        }

        private static Task<string> ReadTokenAsync(HttpResponseMessage response)
        {
            return ReadStringPropertyAsync(response, "token");
        }

        private static async Task<string> ReadStringPropertyAsync(HttpResponseMessage response, string propertyName)
        {
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty(propertyName).GetString() ?? "";
        }

        /// <summary>
        /// A validation service whose login issues a token carrying a claim too large for any cookie.
        /// </summary>
        private sealed class LargeTokenValidationService : IAuthRequestValidationService
        {
            public Task<ApiKeyAuthResult> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
            {
                return Task.FromResult(ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey, attemptedClientId: request.ClientId));
            }

            public Task<LoginResult> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
            {
                DateTime expiresAt = DateTime.UtcNow.AddHours(1);
                string token = jwtTokenService.CreateToken(
                    request.Username, "user", [new Claim("padding", new string('x', 5000))], [], expiresAt);
                return Task.FromResult(LoginResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), request.Username));
            }
        }

        private static string[] SetCookieHeaders(HttpResponseMessage response)
        {
            return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values) ? values.ToArray() : [];
        }

        private static void AssertClearsCookie(HttpResponseMessage response)
        {
            Assert.AreEqual(
                "__Secure-access_token=; max-age=0; path=/files; secure; samesite=strict; httponly",
                SetCookieHeaders(response).Single());
        }
    }
}
