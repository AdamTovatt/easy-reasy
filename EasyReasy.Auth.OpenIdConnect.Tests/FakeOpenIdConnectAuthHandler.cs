using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// An <see cref="IOpenIdConnectAuthHandler"/> that returns preset settings and a preset response, and records what the
    /// sign-in endpoints handed it.
    /// </summary>
    internal sealed class FakeOpenIdConnectAuthHandler : IOpenIdConnectAuthHandler
    {
        /// <summary>
        /// The settings each begin request gets, by provider key; a key missing here gets its provider's sign-in redirect
        /// URI and nothing else.
        /// </summary>
        public Dictionary<string, OpenIdConnectSignInSettings> Settings { get; } = new Dictionary<string, OpenIdConnectSignInSettings>(StringComparer.Ordinal);

        /// <summary>
        /// The keys the handler does not offer for sign-in, answered with null settings.
        /// </summary>
        public HashSet<string> NotOffered { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// The response to sign the user in with, or null to reject the sign-in.
        /// </summary>
        public AuthResponse? ResponseToReturn { get; set; }

        public List<string> SettingsRequestedFor { get; } = new List<string>();

        public List<OpenIdConnectUserInfo> ReceivedUsers { get; } = new List<OpenIdConnectUserInfo>();

        public IRefreshTokenService? LastReceivedRefreshTokenService { get; private set; }

        public Task<OpenIdConnectSignInSettings?> GetSignInSettingsAsync(string providerKey, HttpContext httpContext)
        {
            SettingsRequestedFor.Add(providerKey);

            if (NotOffered.Contains(providerKey))
            {
                return Task.FromResult<OpenIdConnectSignInSettings?>(null);
            }

            return Task.FromResult<OpenIdConnectSignInSettings?>(Settings.TryGetValue(providerKey, out OpenIdConnectSignInSettings? settings)
                ? settings
                : new OpenIdConnectSignInSettings { RedirectUri = OpenIdConnectAuthHarness.SignInRedirectUri(providerKey) });
        }

        public Task<AuthResponse?> HandleOpenIdConnectUserAsync(
            OpenIdConnectUserInfo userInfo,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService? refreshTokenService,
            HttpContext httpContext)
        {
            ReceivedUsers.Add(userInfo);
            LastReceivedRefreshTokenService = refreshTokenService;
            return Task.FromResult(ResponseToReturn);
        }
    }
}
