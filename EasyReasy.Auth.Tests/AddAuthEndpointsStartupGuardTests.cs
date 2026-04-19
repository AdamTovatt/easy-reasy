using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Verifies the fail-fast startup guards in <see cref="AuthApplicationBuilderExtensions.AddAuthEndpoints"/>.
    /// Ensures missing DI registrations throw a clear <see cref="InvalidOperationException"/>, and that the
    /// guards correctly gate on the endpoint enable-flags (so a consumer enabling only one endpoint family
    /// doesn't have to register services for the other).
    /// </summary>
    [TestClass]
    public class AddAuthEndpointsStartupGuardTests
    {
        private const string Secret = "super_secret_key_12345_12345_12345";

        [TestMethod]
        public void AddAuthEndpoints_WithLogoutEnabledAndNoRefreshTokenService_ShouldThrow()
        {
            WebApplicationBuilder builder = CreateBuilder();
            builder.Services.AddSingleton<IAuthRequestValidationService, NoopValidationService>();
            WebApplication app = builder.Build();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => app.AddAuthEndpoints(allowRefresh: false, allowLogout: true));

            StringAssert.Contains(exception.Message, nameof(IRefreshTokenService));
        }

        [TestMethod]
        public void AddAuthEndpoints_WithRefreshEnabledAndNoRefreshTokenService_ShouldThrow()
        {
            WebApplicationBuilder builder = CreateBuilder();
            builder.Services.AddSingleton<IAuthRequestValidationService, NoopValidationService>();
            WebApplication app = builder.Build();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => app.AddAuthEndpoints(allowRefresh: true, allowLogout: false));

            StringAssert.Contains(exception.Message, nameof(IRefreshTokenService));
        }

        [TestMethod]
        public void AddAuthEndpoints_WithApiKeysEnabledAndNoValidationService_ShouldThrow()
        {
            WebApplicationBuilder builder = CreateBuilder();
            WebApplication app = builder.Build();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => app.AddAuthEndpoints(allowApiKeys: true, allowUsernamePassword: false, allowLogout: false));

            StringAssert.Contains(exception.Message, nameof(IAuthRequestValidationService));
        }

        [TestMethod]
        public void AddAuthEndpoints_WithAllApiKeyLoginLogoutDisabled_ShouldNotRequireValidationService()
        {
            // Only refresh enabled; validation service is not needed and should not be checked.
            WebApplicationBuilder builder = CreateBuilder();
            builder.Services.AddRefreshTokenService<FakeRefreshTokenStore>();
            WebApplication app = builder.Build();

            // Should not throw despite no IAuthRequestValidationService registered.
            app.AddAuthEndpoints(allowApiKeys: false, allowUsernamePassword: false, allowRefresh: true, allowLogout: false);
        }

        [TestMethod]
        public void AddAuthEndpoints_WithOnlyLogoutEnabledAndRefreshTokenServiceRegistered_ShouldNotRequireValidationService()
        {
            WebApplicationBuilder builder = CreateBuilder();
            builder.Services.AddRefreshTokenService<FakeRefreshTokenStore>();
            WebApplication app = builder.Build();

            app.AddAuthEndpoints(allowApiKeys: false, allowUsernamePassword: false, allowRefresh: false, allowLogout: true);
        }

        private static WebApplicationBuilder CreateBuilder()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddEasyReasyAuth(Secret);
            return builder;
        }

        private sealed class NoopValidationService : IAuthRequestValidationService
        {
            public Task<ApiKeyAuthResult> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
            {
                return Task.FromResult(ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey));
            }

            public Task<LoginResult> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
            {
                return Task.FromResult(LoginResult.Failed(LoginFailureReason.InvalidCredentials));
            }
        }
    }
}
