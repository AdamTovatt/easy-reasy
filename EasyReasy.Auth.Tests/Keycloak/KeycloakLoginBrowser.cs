using System.Net;
using System.Text.RegularExpressions;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A person's browser at Keycloak: opens where a sign-in sends it, follows Keycloak's redirects, and fills in the login
    /// form when Keycloak shows one, until a redirect leaves for the application or Keycloak shows another page. Keeps
    /// Keycloak's cookies, so a second sign-in reuses the session unless it forces authentication. Linked into every test
    /// project with Keycloak tests, each reading its protocol's response from where this stops.
    /// </summary>
    /// <remarks>
    /// The cookies are kept in a <see cref="FakeBrowser"/> and redirects are followed here: Keycloak marks its session
    /// cookies <c>Secure</c>, which a <see cref="CookieContainer"/> never sends to the plain-http container.
    /// </remarks>
    internal sealed partial class KeycloakLoginBrowser : IDisposable
    {
        private const int MaxRedirects = 10;

        private readonly string _username;
        private readonly string _password;
        private readonly HttpClient _client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
        private readonly FakeBrowser _cookies = new FakeBrowser(TimeProvider.System);

        /// <summary>
        /// A browser that signs in as <paramref name="username"/> with <paramref name="password"/>.
        /// </summary>
        public KeycloakLoginBrowser(string username, string password)
        {
            _username = username;
            _password = password;
        }

        /// <summary>
        /// Whether the last <see cref="SignInAsync"/> had to fill in the login form.
        /// </summary>
        public bool LoginFormShown { get; private set; }

        /// <summary>
        /// Opens <paramref name="url"/> and signs in, logging in when Keycloak asks.
        /// </summary>
        /// <param name="url">Where the sign-in sends the browser.</param>
        /// <param name="leavesForApplication">Whether a redirect goes to the application, which this browser stops at
        /// without loading; null when the response comes back on a page instead.</param>
        /// <param name="what">What <paramref name="url"/> is, for failure messages.</param>
        /// <returns>The redirect to the application, or the page Keycloak showed after any login.</returns>
        public async Task<Stop> SignInAsync(string url, Func<Uri, bool>? leavesForApplication, string what)
        {
            LoginFormShown = false;
            Stop stop = await NavigateAsync(new HttpRequestMessage(HttpMethod.Get, url), leavesForApplication, what);
            if (stop.Page is not string page || FindLoginAction(page) is not string loginAction)
            {
                return stop;
            }

            LoginFormShown = true;
            HttpRequestMessage login = new HttpRequestMessage(HttpMethod.Post, loginAction)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["username"] = _username,
                    ["password"] = _password,
                    ["credentialId"] = string.Empty,
                }),
            };
            return await NavigateAsync(login, leavesForApplication, "the login form");
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private async Task<Stop> NavigateAsync(HttpRequestMessage request, Func<Uri, bool>? leavesForApplication, string what)
        {
            for (int redirect = 0; ; redirect++)
            {
                _cookies.ApplyTo(request);
                HttpResponseMessage response = await _client.SendAsync(request);
                _cookies.Absorb(response);

                if (response.Headers.Location is Uri location && (int)response.StatusCode is >= 300 and < 400)
                {
                    Uri target = new Uri(request.RequestUri!, location);
                    if (leavesForApplication?.Invoke(target) == true)
                    {
                        return new Stop(target, null);
                    }

                    Assert.IsTrue(redirect < MaxRedirects, $"Keycloak redirected {what} more than {MaxRedirects} times.");
                    request = new HttpRequestMessage(HttpMethod.Get, target);
                    continue;
                }

                string page = await response.Content.ReadAsStringAsync();
                Assert.IsTrue(response.IsSuccessStatusCode, $"Keycloak answered {what} with {(int)response.StatusCode}:\n{page}");
                return new Stop(null, page);
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

        [GeneratedRegex("<form\\b[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex FormTagPattern();

        [GeneratedRegex("\\baction=\"(?<action>[^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex ActionPattern();

        /// <summary>
        /// Where a sign-in stopped: at a redirect to the application, or on a page Keycloak showed.
        /// </summary>
        public sealed record Stop(Uri? Landing, string? Page);
    }
}
