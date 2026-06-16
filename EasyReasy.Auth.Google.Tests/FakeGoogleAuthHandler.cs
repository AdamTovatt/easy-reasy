using EasyReasy.Auth.Google;
using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Google.Tests
{
    /// <summary>
    /// Fake implementation of <see cref="IGoogleAuthHandler"/> for testing.
    /// Returns a pre-configured <see cref="AuthResponse"/> and records the received <see cref="GoogleUserInfo"/>.
    /// </summary>
    internal class FakeGoogleAuthHandler : IGoogleAuthHandler
    {
        /// <summary>
        /// The <see cref="AuthResponse"/> to return when <see cref="HandleGoogleUserAsync"/> is called.
        /// Set to null to simulate a rejected user.
        /// </summary>
        public AuthResponse? ResponseToReturn { get; set; }

        /// <summary>
        /// The last <see cref="GoogleUserInfo"/> received by <see cref="HandleGoogleUserAsync"/>.
        /// </summary>
        public GoogleUserInfo? LastReceivedUserInfo { get; private set; }

        /// <summary>
        /// The last <see cref="IRefreshTokenService"/> received by <see cref="HandleGoogleUserAsync"/>,
        /// or null if none was registered in the DI container. Lets a test verify that the endpoint
        /// resolves a registered refresh-token service and passes it through to the handler.
        /// </summary>
        public IRefreshTokenService? LastReceivedRefreshTokenService { get; private set; }

        /// <summary>
        /// Whether <see cref="HandleGoogleUserAsync"/> was called.
        /// </summary>
        public bool WasCalled { get; private set; }

        /// <inheritdoc />
        public Task<AuthResponse?> HandleGoogleUserAsync(
            GoogleUserInfo userInfo,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext? httpContext)
        {
            WasCalled = true;
            LastReceivedUserInfo = userInfo;
            LastReceivedRefreshTokenService = refreshTokenService;
            return Task.FromResult(ResponseToReturn);
        }
    }
}
