using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An <see cref="ISamlAuthHandler"/> that hands out the settings a test sets and records the users it was given.
    /// </summary>
    internal sealed class FakeSamlAuthHandler : ISamlAuthHandler
    {
        /// <summary>
        /// The settings every key gets, or null to offer no key for sign-in.
        /// </summary>
        public SamlSignInSettings? Settings { get; set; } = new SamlSignInSettings { CompletionPath = "/signed-in" };

        public AuthResponse? ResponseToReturn { get; set; }

        public List<string> SettingsRequestedFor { get; } = new List<string>();

        public List<SamlUserInfo> HandledUsers { get; } = new List<SamlUserInfo>();

        public Task<SamlSignInSettings?> GetSignInSettingsAsync(string identityProviderKey, HttpContext httpContext)
        {
            SettingsRequestedFor.Add(identityProviderKey);
            return Task.FromResult(Settings);
        }

        public Task<AuthResponse?> HandleSamlUserAsync(SamlUserInfo userInfo, IJwtTokenService jwtTokenService, IRefreshTokenService? refreshTokenService, HttpContext httpContext)
        {
            HandledUsers.Add(userInfo);
            return Task.FromResult(ResponseToReturn);
        }
    }
}
