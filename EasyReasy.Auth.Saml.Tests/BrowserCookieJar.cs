using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A browser's cookie jar for one origin: keeps what responses set, drops what they expire, and sends the rest, whatever
    /// their <c>Secure</c> flag, so it serves a plain-http test server as well. For the application, the identity
    /// provider's cross-site post to the assertion consumer service is sent without it, as a browser does with
    /// <c>SameSite=Lax</c> cookies; <see cref="KeycloakBrowser"/> keeps one for Keycloak.
    /// </summary>
    internal sealed class BrowserCookieJar
    {
        public Dictionary<string, SetCookieHeaderValue> Cookies { get; } = new Dictionary<string, SetCookieHeaderValue>(StringComparer.Ordinal);

        public void Receive(IEnumerable<string> setCookieHeaders)
        {
            foreach (SetCookieHeaderValue cookie in SetCookieHeaderValue.ParseList(setCookieHeaders.ToList()))
            {
                string name = cookie.Name.Value!;
                bool expired = cookie.Expires is DateTimeOffset expires && expires <= DateTimeOffset.UtcNow || cookie.MaxAge is TimeSpan maxAge && maxAge <= TimeSpan.Zero;
                if (expired || string.IsNullOrEmpty(cookie.Value.Value))
                {
                    Cookies.Remove(name);
                }
                else
                {
                    Cookies[name] = cookie;
                }
            }
        }

        public void Receive(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
            {
                Receive(values);
            }
        }

        public string? CookieHeader => Cookies.Count == 0 ? null : string.Join("; ", Cookies.Values.Select(cookie => $"{cookie.Name}={cookie.Value}"));

        public void Apply(HttpRequestMessage request)
        {
            if (CookieHeader is string header)
            {
                request.Headers.Add("Cookie", header);
            }
        }
    }
}
