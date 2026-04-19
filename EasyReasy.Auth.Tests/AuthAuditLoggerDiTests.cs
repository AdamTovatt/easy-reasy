using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// DI-level tests proving that a registered <see cref="IAuthAuditLogger"/> is resolved and invoked
    /// by the refresh token service wiring in <c>AddRefreshTokenService</c>.
    /// Endpoint-level integration tests are intentionally not included here because the endpoint wiring
    /// simply forwards to the service layer — these tests verify the wiring path that is unique to this library.
    /// </summary>
    [TestClass]
    public class AuthAuditLoggerDiTests
    {
        [TestMethod]
        public async Task RegisteredAuditLogger_ShouldBeInvokedByInvalidateAllSessionsAsync()
        {
            ServiceCollection services = new ServiceCollection();
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            services.AddSingleton<IAuthAuditLogger>(auditLogger);
            services.AddRefreshTokenService<FakeRefreshTokenStore>();

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            IRefreshTokenService refreshService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
            await refreshService.CreateRefreshTokenAsync("user-42", "user", null, null);

            SessionRevocationResult result = await refreshService.InvalidateAllSessionsAsync("user-42");

            Assert.AreEqual(1, auditLogger.SessionsInvalidatedCalls.Count);
            Assert.AreSame(result, auditLogger.SessionsInvalidatedCalls[0]);
            Assert.AreEqual("user-42", result.Subject);
            Assert.AreEqual(1, result.InvalidatedFamilyCount);
        }

        [TestMethod]
        public async Task NoAuditLoggerRegistered_ShouldNotThrow()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddRefreshTokenService<FakeRefreshTokenStore>();

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            IRefreshTokenService refreshService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
            await refreshService.CreateRefreshTokenAsync("user-42", "user", null, null);

            SessionRevocationResult result = await refreshService.InvalidateAllSessionsAsync("user-42");

            Assert.AreEqual("user-42", result.Subject);
            Assert.AreEqual(1, result.InvalidatedFamilyCount);
        }

        [TestMethod]
        public void DefaultInterfaceMethods_ShouldBeCallableAsNoOps()
        {
            // Verifies that a consumer implementing only the method(s) they care about does not have to
            // implement the others — the default interface methods are no-ops that complete successfully.
            MinimalAuditLogger logger = new MinimalAuditLogger();
            IAuthAuditLogger asInterface = logger;

            DefaultHttpContext httpContext = new DefaultHttpContext();

            Assert.IsTrue(asInterface.OnLoginAsync(httpContext, LoginResult.Failed(LoginFailureReason.InvalidCredentials)).IsCompletedSuccessfully);
            Assert.IsTrue(asInterface.OnApiKeyAuthAsync(httpContext, ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey)).IsCompletedSuccessfully);
            Assert.IsTrue(asInterface.OnRefreshAsync(httpContext, RefreshResult.Failed(RefreshFailureReason.TokenExpired)).IsCompletedSuccessfully);
            Assert.IsTrue(asInterface.OnLogoutAsync(httpContext, LogoutResult.Unknown()).IsCompletedSuccessfully);
        }

        private sealed class MinimalAuditLogger : IAuthAuditLogger
        {
            // Implements the interface without overriding any default methods.
        }
    }
}
