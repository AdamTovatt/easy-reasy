using System.Net;
using System.Text.RegularExpressions;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A person's browser at Keycloak, signed in as the realms' one user: follows an AuthnRequest redirect, logs in when
    /// Keycloak asks, and reads the <c>SAMLResponse</c> from the page that would post it to the assertion consumer
    /// service. Keeps Keycloak's cookies, so a second sign-in reuses the session unless the request forces authentication.
    /// </summary>
    internal sealed partial class KeycloakBrowser : IDisposable
    {
        private readonly KeycloakLoginBrowser _browser = new KeycloakLoginBrowser(KeycloakServer.Username, KeycloakServer.Password);

        /// <summary>
        /// Whether the last <see cref="SignInAsync"/> had to fill in the login form.
        /// </summary>
        public bool LoginFormShown => _browser.LoginFormShown;

        /// <summary>
        /// Signs in at Keycloak for the AuthnRequest in <paramref name="redirectUrl"/> and returns the response Keycloak
        /// would post to the assertion consumer service.
        /// </summary>
        public async Task<string> SignInAsync(string redirectUrl)
        {
            // Keycloak answers on a page that posts the response, so the browser never leaves for the application here.
            KeycloakLoginBrowser.Stop stop = await _browser.SignInAsync(redirectUrl, leavesForApplication: null, "the AuthnRequest redirect");
            Match response = SamlResponsePattern().Match(stop.Page ?? string.Empty);
            return response.Success
                ? WebUtility.HtmlDecode(response.Groups["value"].Value)
                : throw new AssertFailedException($"Keycloak answered without a response:\n{stop.Page}");
        }

        public void Dispose()
        {
            _browser.Dispose();
        }

        [GeneratedRegex("name=\"SAMLResponse\"\\s+value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex SamlResponsePattern();
    }
}
