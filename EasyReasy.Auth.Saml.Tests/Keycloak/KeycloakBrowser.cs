using System.Net;
using System.Text.RegularExpressions;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A person's browser at Keycloak: follows an AuthnRequest redirect, fills in the login form when Keycloak shows one,
    /// and reads the <c>SAMLResponse</c> from the page that would post it to the assertion consumer service. Keeps
    /// Keycloak's cookies, so a second sign-in reuses the session unless the request forces authentication.
    /// </summary>
    /// <remarks>
    /// The cookies are kept in a <see cref="BrowserCookieJar"/> and redirects are followed here: Keycloak marks its session
    /// cookies <c>Secure</c>, which a <see cref="CookieContainer"/> never sends to the plain-http container.
    /// </remarks>
    internal sealed partial class KeycloakBrowser : IDisposable
    {
        private const int MaxRedirects = 10;

        private readonly HttpClient _client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
        private readonly BrowserCookieJar _cookies = new BrowserCookieJar();

        /// <summary>
        /// Whether the last <see cref="SignInAsync"/> had to fill in the login form.
        /// </summary>
        public bool LoginFormShown { get; private set; }

        /// <summary>
        /// Signs in at Keycloak for the AuthnRequest in <paramref name="redirectUrl"/> and returns the response Keycloak
        /// would post to the assertion consumer service.
        /// </summary>
        public async Task<string> SignInAsync(string redirectUrl)
        {
            string page = await SendAsync(new HttpRequestMessage(HttpMethod.Get, redirectUrl), "the AuthnRequest redirect");
            LoginFormShown = false;
            if (FindSamlResponse(page) is string fromSession)
            {
                return fromSession;
            }

            string? loginAction = FindLoginAction(page);
            Assert.IsNotNull(loginAction, $"Keycloak showed neither a login form nor a response:\n{page}");
            LoginFormShown = true;

            HttpRequestMessage login = new HttpRequestMessage(HttpMethod.Post, loginAction)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["username"] = KeycloakServer.Username,
                    ["password"] = KeycloakServer.Password,
                    ["credentialId"] = string.Empty,
                }),
            };
            string afterLogin = await SendAsync(login, "the login form");

            return FindSamlResponse(afterLogin) ?? throw new AssertFailedException($"Keycloak answered the login without a response:\n{afterLogin}");
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private async Task<string> SendAsync(HttpRequestMessage request, string what)
        {
            for (int redirect = 0; ; redirect++)
            {
                _cookies.Apply(request);
                HttpResponseMessage response = await _client.SendAsync(request);
                _cookies.Receive(response);

                if (response.Headers.Location is Uri location && (int)response.StatusCode is >= 300 and < 400)
                {
                    Assert.IsTrue(redirect < MaxRedirects, $"Keycloak redirected {what} more than {MaxRedirects} times.");
                    request = new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri!, location));
                    continue;
                }

                string page = await response.Content.ReadAsStringAsync();
                Assert.IsTrue(response.IsSuccessStatusCode, $"Keycloak answered {what} with {(int)response.StatusCode}:\n{page}");
                return page;
            }
        }

        private static string? FindLoginAction(string page)
        {
            foreach (Match form in FormTagPattern().Matches(page))
            {
                if (form.Value.Contains("id=\"kc-form-login\"", StringComparison.Ordinal) && ActionPattern().Match(form.Value) is { Success: true } action)
                {
                    return WebUtility.HtmlDecode(action.Groups["action"].Value);
                }
            }

            return null;
        }

        private static string? FindSamlResponse(string page)
        {
            Match match = SamlResponsePattern().Match(page);
            return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : null;
        }

        [GeneratedRegex("<form\\b[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex FormTagPattern();

        [GeneratedRegex("\\baction=\"(?<action>[^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex ActionPattern();

        [GeneratedRegex("name=\"SAMLResponse\"\\s+value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex SamlResponsePattern();
    }
}
