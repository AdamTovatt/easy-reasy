using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Text.RegularExpressions;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// A person's browser at Keycloak: follows the authorize URL, fills in the login form, and stops at Keycloak's
    /// redirect to the application's redirect URI, returning what that page would post to the application.
    /// </summary>
    /// <remarks>
    /// The cookies are kept in a <see cref="FakeBrowser"/> and redirects are followed here: Keycloak marks its session
    /// cookies <c>Secure</c>, which a <see cref="CookieContainer"/> never sends to the plain-http container.
    /// </remarks>
    internal sealed partial class KeycloakBrowser : IDisposable
    {
        private const int MaxRedirects = 10;

        private readonly HttpClient _client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
        private readonly FakeBrowser _cookies = new FakeBrowser(TimeProvider.System);

        /// <summary>
        /// Signs in at Keycloak for the flow in <paramref name="authorizationUrl"/> and returns the response its redirect
        /// brings to <paramref name="redirectUri"/>: the code, the state and the <c>iss</c> parameter.
        /// </summary>
        public async Task<OpenIdConnectAuthorizationResponse> SignInAsync(string authorizationUrl, string redirectUri)
        {
            BrowserStop stop = await NavigateAsync(new HttpRequestMessage(HttpMethod.Get, authorizationUrl), redirectUri, "the authorize URL");
            if (stop.Landing == null)
            {
                string? loginAction = FindLoginAction(stop.Page!);
                Assert.IsNotNull(loginAction, $"Keycloak showed neither a login form nor a redirect:\n{stop.Page}");

                HttpRequestMessage login = new HttpRequestMessage(HttpMethod.Post, loginAction)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["username"] = KeycloakServer.Username,
                        ["password"] = KeycloakServer.Password,
                        ["credentialId"] = string.Empty,
                    }),
                };
                stop = await NavigateAsync(login, redirectUri, "the login form");
            }

            Uri landing = stop.Landing ?? throw new AssertFailedException($"Keycloak answered the login without redirecting:\n{stop.Page}");
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
            _client.Dispose();
        }

        /// <summary>
        /// Follows redirects until one leads to the application's redirect URI, which this browser does not load, or a
        /// page is shown.
        /// </summary>
        private async Task<BrowserStop> NavigateAsync(HttpRequestMessage request, string redirectUri, string what)
        {
            for (int redirect = 0; ; redirect++)
            {
                _cookies.ApplyTo(request);
                HttpResponseMessage response = await _client.SendAsync(request);
                _cookies.Absorb(response);

                if (response.Headers.Location is Uri location && (int)response.StatusCode is >= 300 and < 400)
                {
                    Uri target = new Uri(request.RequestUri!, location);
                    if (target.GetLeftPart(UriPartial.Path) == redirectUri)
                    {
                        return new BrowserStop(target, null);
                    }

                    Assert.IsTrue(redirect < MaxRedirects, $"Keycloak redirected {what} more than {MaxRedirects} times.");
                    request = new HttpRequestMessage(HttpMethod.Get, target);
                    continue;
                }

                string page = await response.Content.ReadAsStringAsync();
                Assert.IsTrue(response.IsSuccessStatusCode, $"Keycloak answered {what} with {(int)response.StatusCode}:\n{page}");
                return new BrowserStop(null, page);
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
        /// Where navigation stopped: at the application's redirect URI, or on a page Keycloak showed.
        /// </summary>
        private sealed record BrowserStop(Uri? Landing, string? Page);
    }
}
