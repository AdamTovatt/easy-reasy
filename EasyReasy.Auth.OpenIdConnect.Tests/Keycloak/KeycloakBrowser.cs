using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// A person's browser at Keycloak, signed in as the realm's one user: follows the authorize URL, logs in when Keycloak
    /// asks, and stops at Keycloak's redirect to the application's redirect URI, returning what that page would post to
    /// the application.
    /// </summary>
    internal sealed class KeycloakBrowser : IDisposable
    {
        private readonly KeycloakLoginBrowser _browser = new KeycloakLoginBrowser(KeycloakServer.Username, KeycloakServer.Password);

        /// <summary>
        /// Signs in at Keycloak for the flow in <paramref name="authorizationUrl"/> and returns the response its redirect
        /// brings to <paramref name="redirectUri"/>: the code, the state and the <c>iss</c> parameter.
        /// </summary>
        public async Task<OpenIdConnectAuthorizationResponse> SignInAsync(string authorizationUrl, string redirectUri)
        {
            KeycloakLoginBrowser.Stop stop = await _browser.SignInAsync(
                authorizationUrl,
                target => target.GetLeftPart(UriPartial.Path) == redirectUri,
                "the authorize URL");

            Uri landing = stop.Landing ?? throw new AssertFailedException($"Keycloak showed a page instead of redirecting:\n{stop.Page}");
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(landing.Query);
            Assert.IsFalse(query.ContainsKey("error"), $"Keycloak redirected with an error: {landing}");
            return new OpenIdConnectAuthorizationResponse(
                query["code"].ToString(),
                query["state"].ToString(),
                query.TryGetValue("iss", out StringValues issuer) ? issuer.ToString() : null,
                redirectUri);
        }

        public void Dispose()
        {
            _browser.Dispose();
        }
    }
}
