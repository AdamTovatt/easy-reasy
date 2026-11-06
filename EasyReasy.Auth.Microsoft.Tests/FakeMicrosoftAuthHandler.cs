using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// An <see cref="IMicrosoftAuthHandler"/> that returns preset settings and a preset response, and records what the
    /// sign-in endpoint handed it.
    /// </summary>
    internal sealed class FakeMicrosoftAuthHandler : IMicrosoftAuthHandler
    {
        /// <summary>
        /// The settings every begin request gets.
        /// </summary>
        public MicrosoftSignInSettings Settings { get; set; } = new MicrosoftSignInSettings
        {
            RedirectUri = MicrosoftAuthHarness.SignInRedirectUri,
        };

        /// <summary>
        /// The response to sign the user in with, or null to reject the sign-in.
        /// </summary>
        public AuthResponse? ResponseToReturn { get; set; }

        public List<MicrosoftUserInfo> ReceivedUsers { get; } = new List<MicrosoftUserInfo>();

        public IRefreshTokenService? LastReceivedRefreshTokenService { get; private set; }

        /// <summary>
        /// The path of each request the handler was called for, settings and user calls alike, in order.
        /// </summary>
        public List<string> ReceivedRequestPaths { get; } = new List<string>();

        public Task<MicrosoftSignInSettings> GetSignInSettingsAsync(HttpContext httpContext)
        {
            ReceivedRequestPaths.Add(httpContext.Request.Path);
            return Task.FromResult(Settings);
        }

        public Task<AuthResponse?> HandleMicrosoftUserAsync(
            MicrosoftUserInfo userInfo,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext httpContext)
        {
            ReceivedRequestPaths.Add(httpContext.Request.Path);
            ReceivedUsers.Add(userInfo);
            LastReceivedRefreshTokenService = refreshTokenService;
            return Task.FromResult(ResponseToReturn);
        }
    }
}
