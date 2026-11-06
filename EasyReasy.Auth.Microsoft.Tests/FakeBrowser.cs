using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// A cookie jar standing in for one browser: it keeps what responses set, drops what they expire or what outlives
    /// its <c>Max-Age</c> or <c>Expires</c> on the test's clock, and sends the rest with each request. Two instances
    /// are two browsers.
    /// </summary>
    internal sealed class FakeBrowser
    {
        private readonly TimeProvider _clock;
        private readonly Dictionary<string, StoredCookie> _cookies = new Dictionary<string, StoredCookie>(StringComparer.Ordinal);

        public FakeBrowser(TimeProvider clock)
        {
            _clock = clock;
        }

        /// <summary>
        /// The cookies the browser would send now, by name.
        /// </summary>
        public IReadOnlyDictionary<string, string> Cookies => _cookies
            .Where(cookie => cookie.Value.ExpiresAt is not DateTimeOffset expiresAt || expiresAt > _clock.GetUtcNow())
            .ToDictionary(cookie => cookie.Key, cookie => cookie.Value.Value, StringComparer.Ordinal);

        public void Absorb(IEnumerable<string> setCookieHeaders)
        {
            DateTimeOffset now = _clock.GetUtcNow();

            foreach (SetCookieHeaderValue cookie in SetCookieHeaderValue.ParseList(setCookieHeaders.ToList()))
            {
                // Max-Age wins over Expires, as in a browser.
                DateTimeOffset? expiresAt = cookie.MaxAge is TimeSpan maxAge ? now + maxAge : cookie.Expires;
                string name = cookie.Name.ToString();

                if (expiresAt is DateTimeOffset expiry && expiry <= now)
                {
                    _cookies.Remove(name);
                }
                else
                {
                    _cookies[name] = new StoredCookie(cookie.Value.ToString(), expiresAt);
                }
            }
        }

        public void Absorb(HttpResponse response)
        {
            Absorb(response.Headers.SetCookie.Select(value => value ?? ""));
        }

        public void Absorb(HttpResponseMessage response)
        {
            Absorb(response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values) ? values : []);
        }

        /// <summary>
        /// Plants a cookie, as an attacker's page or another subdomain might, with no expiry.
        /// </summary>
        public void Set(string name, string value)
        {
            _cookies[name] = new StoredCookie(value, null);
        }

        public void ApplyTo(HttpRequest request)
        {
            if (CreateCookieHeader() is string header)
            {
                request.Headers.Cookie = header;
            }
        }

        public void ApplyTo(HttpRequestMessage request)
        {
            if (CreateCookieHeader() is string header)
            {
                request.Headers.Add("Cookie", header);
            }
        }

        private string? CreateCookieHeader()
        {
            IReadOnlyDictionary<string, string> cookies = Cookies;
            return cookies.Count == 0 ? null : string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}"));
        }

        private sealed record StoredCookie(string Value, DateTimeOffset? ExpiresAt);
    }
}
