using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

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
        [TestMethod]
        public async Task LoginEndpoint_OnSuccess_ShouldInvokeOnLoginAsyncWithSucceededResult()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: true));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/login", new LoginAuthRequest("alice", "correct-horse"));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.AreEqual(1, auditLogger.LoginCalls.Count);
            Assert.IsTrue(auditLogger.LoginCalls[0].Result.Success);
            Assert.AreEqual("alice", auditLogger.LoginCalls[0].Result.AttemptedSubject);
        }

        [TestMethod]
        public async Task LoginEndpoint_OnFailure_ShouldInvokeOnLoginAsyncWithFailedResultAndAttemptedSubject()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

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
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: true));

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
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: false));

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
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: true));

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
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: true));

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
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger, new StubValidationService(succeed: true), store);

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
            await using HostedAuthApp host = await HostedAuthApp.StartAsync(auditLogger: null, new StubValidationService(succeed: true));

            HttpResponseMessage response = await host.Client.PostAsJsonAsync(
                "/api/auth/logout", new LogoutRequest("anything"));

            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
