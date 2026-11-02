using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The <c>__Secure-access_token</c> cookie: reads the bearer token from it on a <c>GET</c> or <c>HEAD</c> request on
    /// a configured path, for the plain navigations (a file opened in a new tab) a browser cannot attach an
    /// <c>Authorization</c> header to, and writes and expires it.
    /// </summary>
    /// <remarks>
    /// Reading and writing live together because they share one contract: the cookie's name, the prefixes it is
    /// scoped to, and the attributes that make it safe to honour. <see cref="AuthServiceCollectionExtensions"/>
    /// registers one instance, with an empty prefix list when the option is off. Its full name is also the logger
    /// category of the error <see cref="TryWriteCookies"/> logs, <c>EasyReasy.Auth.AccessTokenCookie</c>, which the
    /// README tells consumers to watch, so renaming this class changes a public contract.
    /// </remarks>
    internal sealed class AccessTokenCookie : IBearerTokenSource
    {
        /// <summary>
        /// The cookie's fixed name. The <c>__Secure-</c> prefix makes a browser refuse it unless it is set with
        /// <c>Secure</c> from a secure origin, which <c>http://localhost</c> counts as.
        /// </summary>
        internal const string CookieName = "__Secure-access_token";

        /// <summary>
        /// The largest cookie, name and value together, that browsers are required to store. A larger one is
        /// dropped silently, so writing it would leave a sign-in that cannot open a tab and no error anywhere.
        /// </summary>
        internal const int MaxCookieBytes = 4096;

        /// <summary>
        /// The advice that ends both the exception and the log entry for a token too large for the cookie.
        /// </summary>
        private const string FewerClaimsAdvice = "Put fewer claims on tokens issued to browsers.";

        private readonly PathPrefixList _paths;

        internal AccessTokenCookie(IEnumerable<string> paths)
        {
            _paths = new PathPrefixList(paths);
        }

        /// <inheritdoc />
        public string OptionName => nameof(EasyReasyAuthOptions.CookieTokenPaths);

        /// <summary>
        /// Whether any prefix is listed, so the cookie is read and may be written.
        /// </summary>
        internal bool IsEnabled => _paths.Prefixes.Count > 0;

        /// <summary>
        /// Returns the cookie token of a <c>GET</c> or <c>HEAD</c> request on a configured path that is not a protocol
        /// upgrade and carries the cookie exactly once, or <c>null</c> when the request does not qualify.
        /// </summary>
        public string? ReadToken(HttpRequest request)
        {
            // Only reads: a same-site page (a sibling subdomain counts) can still make the browser send the cookie,
            // so honouring it on a state-changing method would hand that page the user's authority.
            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            {
                return null;
            }

            // A WebSocket handshake is a GET, but it opens a two-way channel rather than a read, and browsers send a
            // SameSite=Strict cookie on a handshake from any same-site page with no CORS check. Read from the headers,
            // not only IsWebSocketRequest, because the WebSocket middleware that sets that may run after
            // authentication. (An HTTP/2 WebSocket is a CONNECT, which the method check has already refused.)
            if (IsUpgradeRequest(request))
            {
                return null;
            }

            if (!_paths.Matches(request.Path))
            {
                return null;
            }

            // Parsed from the raw headers because Request.Cookies keeps only one of several same-named cookies. A
            // second one means a sibling subdomain may have tossed its own, and which one is the user's cannot be
            // told, so neither is used. The parser skips malformed neighbours rather than failing the whole header,
            // and names compare ordinally, as browsers store them.
            // OfType drops the null entries StringValues may hold. A Cookie header split across several fields
            // (HTTP/2 may send one per cookie) is parsed as one list.
            List<string> cookieHeaders = request.Headers.Cookie.OfType<string>().ToList();

            if (!CookieHeaderValue.TryParseList(cookieHeaders, out IList<CookieHeaderValue>? cookies))
            {
                return null;
            }

            CookieHeaderValue[] matches = cookies
                .Where(cookie => StringSegment.Equals(cookie.Name, CookieName, StringComparison.Ordinal))
                .ToArray();

            return matches.Length == 1 ? matches[0].Value.Value : null;
        }

        /// <summary>
        /// Appends one <c>Set-Cookie</c> header per configured prefix carrying <paramref name="accessToken"/>, each
        /// living exactly as long as the token's own <c>exp</c>, measured on <paramref name="timeProvider"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the token is not a JWT or has no <c>exp</c> claim.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cookie would exceed <see cref="MaxCookieBytes"/>.</exception>
        internal void WriteCookies(HttpContext context, string accessToken, TimeProvider timeProvider)
        {
            long maxAgeSeconds = ReadSecondsUntilExpiry(accessToken, timeProvider);

            if (!FitsInCookie(accessToken, out int cookieBytes))
            {
                throw new InvalidOperationException(
                    $"{DescribeOversizedCookie(cookieBytes)} A browser would drop it silently. {FewerClaimsAdvice}");
            }

            AppendCookies(context, accessToken, maxAgeSeconds);
        }

        /// <summary>
        /// Writes the cookie as <see cref="WriteCookies"/> does, except that a token too large for a cookie expires
        /// the cookie instead, logs an error naming the size and the limit, and returns <c>false</c>.
        /// </summary>
        /// <remarks>
        /// For an endpoint that has already committed the session when it writes the cookie, such as a refresh that
        /// has used up the old refresh token: failing the response there would lose the new one. Expiring the cookie
        /// keeps it from carrying an older token than the session's. Only the size is tolerated, since claims can
        /// grow between refreshes; a token without <c>exp</c> is a bug in the issuer and still throws.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the token is not a JWT or has no <c>exp</c> claim.</exception>
        internal bool TryWriteCookies(HttpContext context, string accessToken, TimeProvider timeProvider, ILogger<AccessTokenCookie> logger)
        {
            long maxAgeSeconds = ReadSecondsUntilExpiry(accessToken, timeProvider);

            if (!FitsInCookie(accessToken, out int cookieBytes))
            {
                ClearCookies(context);
                logger.LogError(
                    "{Problem} It was cleared instead of written. The sign-in succeeded, but this browser cannot open cookie-path tabs until a token fits. {Advice}",
                    DescribeOversizedCookie(cookieBytes),
                    FewerClaimsAdvice);
                return false;
            }

            AppendCookies(context, accessToken, maxAgeSeconds);
            return true;
        }

        /// <summary>
        /// Appends one <c>Set-Cookie</c> header per configured prefix that expires the cookie at once.
        /// </summary>
        internal void ClearCookies(HttpContext context)
        {
            foreach (PathString path in _paths.Prefixes)
            {
                AppendCookie(context, value: "", TimeSpan.Zero, path);
            }
        }

        /// <summary>
        /// Whether the request asks to switch protocols: an <c>Upgrade</c> header of any kind, a WebSocket key, or a
        /// request the WebSocket middleware has already recognised.
        /// </summary>
        private static bool IsUpgradeRequest(HttpRequest request)
        {
            return request.Headers.ContainsKey(HeaderNames.Upgrade)
                || request.Headers.ContainsKey(HeaderNames.SecWebSocketKey)
                || request.HttpContext.WebSockets.IsWebSocketRequest;
        }

        /// <summary>
        /// The sentence naming the size and the limit, shared by the exception and the log entry so they cannot drift.
        /// </summary>
        private static string DescribeOversizedCookie(int cookieBytes)
        {
            return $"The access-token cookie would be {cookieBytes} bytes (name and value), over the {MaxCookieBytes}-byte limit browsers store.";
        }

        /// <summary>
        /// Measures the cookie's name and value together against <see cref="MaxCookieBytes"/>.
        /// </summary>
        private static bool FitsInCookie(string accessToken, out int cookieBytes)
        {
            cookieBytes = Encoding.UTF8.GetByteCount(CookieName) + Encoding.UTF8.GetByteCount(accessToken);
            return cookieBytes <= MaxCookieBytes;
        }

        private void AppendCookies(HttpContext context, string accessToken, long maxAgeSeconds)
        {
            foreach (PathString path in _paths.Prefixes)
            {
                AppendCookie(context, accessToken, TimeSpan.FromSeconds(maxAgeSeconds), path);
            }
        }

        /// <summary>
        /// Appends one <c>Set-Cookie</c> header with the fixed attributes.
        /// </summary>
        /// <remarks>
        /// Written as a raw header rather than through <c>Response.Cookies</c>, so a consumer's cookie policy
        /// middleware cannot drop it for consent or strip <c>Secure</c>: the attributes are the contract.
        /// </remarks>
        private static void AppendCookie(HttpContext context, string value, TimeSpan maxAge, PathString path)
        {
            SetCookieHeaderValue cookie = new SetCookieHeaderValue(CookieName, value)
            {
                // Max-Age rather than Expires, so a client clock that is off cannot move the expiry.
                MaxAge = maxAge,

                // The browser matches the full URL path, while the reader compares Request.Path, which excludes
                // PathBase. No Domain, so the cookie is host-only and no sibling subdomain receives it.
                Path = context.Request.PathBase.Add(path).ToUriComponent(),
                Secure = true,
                HttpOnly = true,
                SameSite = Microsoft.Net.Http.Headers.SameSiteMode.Strict,
            };

            context.Response.Headers.Append(HeaderNames.SetCookie, cookie.ToString());
        }

        /// <summary>
        /// Reads the token's <c>exp</c> without validating the token and returns the whole seconds left until it,
        /// never negative, so the cookie never outlives the token.
        /// </summary>
        private static long ReadSecondsUntilExpiry(string accessToken, TimeProvider timeProvider)
        {
            JsonWebTokenHandler handler = new JsonWebTokenHandler();

            if (!handler.CanReadToken(accessToken))
            {
                throw new ArgumentException("The access token is not a JWT, so its expiry cannot be read.", nameof(accessToken));
            }

            JsonWebToken token = handler.ReadJsonWebToken(accessToken);

            if (!token.TryGetClaim(JwtRegisteredClaimNames.Exp, out _))
            {
                throw new ArgumentException(
                    "The access token has no exp claim, so the cookie's lifetime cannot be bounded; validation would reject the token anyway.",
                    nameof(accessToken));
            }

            DateTimeOffset expiresAt = new DateTimeOffset(DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc));
            double secondsLeft = Math.Floor((expiresAt - timeProvider.GetUtcNow()).TotalSeconds);

            return secondsLeft > 0 ? (long)secondsLeft : 0;
        }
    }
}
