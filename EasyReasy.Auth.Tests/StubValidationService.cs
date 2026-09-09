using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// In-memory <see cref="IAuthRequestValidationService"/> that succeeds or fails every request, records how
    /// many times it was called (so tests can assert that a request never reaches it), and can be pointed at a
    /// specific <see cref="LoginFailureReason"/>. Used across multiple test classes.
    /// </summary>
    internal sealed class StubValidationService : IAuthRequestValidationService
    {
        private readonly bool _succeed;
        private readonly LoginFailureReason _loginFailureReason;
        private readonly ApiKeyAuthFailureReason _apiKeyFailureReason;

        /// <summary>
        /// The number of times <see cref="ValidateLoginRequestAsync"/> was called.
        /// </summary>
        public int LoginCallCount { get; private set; }

        /// <summary>
        /// The number of times <see cref="ValidateApiKeyRequestAsync"/> was called.
        /// </summary>
        public int ApiKeyCallCount { get; private set; }

        public StubValidationService(
            bool succeed,
            LoginFailureReason loginFailureReason = LoginFailureReason.InvalidCredentials,
            ApiKeyAuthFailureReason apiKeyFailureReason = ApiKeyAuthFailureReason.UnknownKey)
        {
            _succeed = succeed;
            _loginFailureReason = loginFailureReason;
            _apiKeyFailureReason = apiKeyFailureReason;
        }

        public Task<ApiKeyAuthResult> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
        {
            ApiKeyCallCount++;

            if (_succeed)
            {
                DateTime expiresAt = DateTime.UtcNow.AddHours(1);
                string token = jwtTokenService.CreateToken("test-client", "apikey", new List<Claim>(), new List<string>(), expiresAt);
                return Task.FromResult(ApiKeyAuthResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), "test-client"));
            }

            // The client id, never the key itself: a result object must not carry a raw credential.
            return Task.FromResult(ApiKeyAuthResult.Failed(_apiKeyFailureReason, attemptedClientId: request.ClientId));
        }

        public Task<LoginResult> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
        {
            LoginCallCount++;

            if (_succeed)
            {
                DateTime expiresAt = DateTime.UtcNow.AddHours(1);
                string token = jwtTokenService.CreateToken(request.Username, "user", new List<Claim>(), new List<string>(), expiresAt);
                return Task.FromResult(LoginResult.Succeeded(new AuthResponse(token, expiresAt.ToString("o")), request.Username));
            }

            return Task.FromResult(LoginResult.Failed(_loginFailureReason, attemptedSubject: request.Username));
        }
    }
}
