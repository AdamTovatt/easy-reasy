using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Configuration options for EasyReasy JWT authentication and authorization.
    /// </summary>
    public class EasyReasyAuthOptions
    {
        /// <summary>
        /// The expected issuer for JWT tokens. If null (default), issuer validation is disabled.
        /// </summary>
        public string? Issuer { get; set; }

        /// <summary>
        /// The expected audience for JWT tokens. If null (default), audience validation is disabled.
        /// When set, tokens must contain a matching <c>aud</c> claim to be accepted.
        /// This prevents tokens issued for one service from being accepted by another.
        /// </summary>
        public string? Audience { get; set; }

        /// <summary>
        /// The clock skew tolerance for token lifetime validation. Default is 30 seconds.
        /// The <c>Microsoft.IdentityModel</c> default is 5 minutes, which is often too generous.
        /// Increase this if you see tokens rejected due to clock drift between servers.
        /// </summary>
        public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether to automatically register <see cref="IJwtTokenService"/> in the DI container.
        /// Default is <c>true</c>. Set to <c>false</c> if you want to register your own implementation.
        /// </summary>
        public bool RegisterJwtTokenService { get; set; } = true;

        /// <summary>
        /// The path prefixes on which a token may arrive in the <c>access_token</c> query-string parameter
        /// instead of the <c>Authorization</c> header. Empty by default, so a token in the query string is
        /// ignored everywhere.
        /// <para>
        /// This exists for WebSocket connections: a browser's <c>WebSocket</c> cannot send an
        /// <c>Authorization</c> header. A prefix matches by whole path segments, ignoring case, so <c>/ws</c>
        /// matches <c>/ws/city</c> but not <c>/wsx</c>, and it is compared against <c>HttpRequest.Path</c>, which
        /// excludes any <c>PathBase</c>. Each prefix must start with <c>/</c> and must not end with one.
        /// </para>
        /// <para>
        /// The query string is read only when the request carries no <c>Authorization</c> header at all,
        /// and the token is validated exactly as a header token is. A token in a URL can end up in access
        /// logs, proxy logs and browser history, so list only the paths that need it.
        /// </para>
        /// </summary>
        public IReadOnlyList<string> QueryStringTokenPaths { get; set; } = [];

        /// <summary>
        /// The path prefixes on which a <c>GET</c> or <c>HEAD</c> request may carry its token in the
        /// <c>__Secure-access_token</c> cookie instead of the <c>Authorization</c> header. Empty by default, so no
        /// cookie is read anywhere.
        /// <para>
        /// This exists for plain navigations, such as a protected file opened in a new tab, which cannot send an
        /// <c>Authorization</c> header. The built-in login, refresh and logout endpoints set and clear the cookie
        /// when this list is non-empty. An endpoint of your own calls
        /// <see cref="HttpContextExtensions.SetAccessTokenCookie"/> where it issues a token,
        /// <see cref="HttpContextExtensions.TrySetAccessTokenCookie"/> where the session is already committed (after a
        /// refresh), and <see cref="HttpContextExtensions.ClearAccessTokenCookie"/> where it ends or refuses a session.
        /// One cookie is written per prefix, with <c>Path</c> set to <c>PathBase</c> plus the prefix.
        /// </para>
        /// <para>
        /// Prefixes match as for <see cref="QueryStringTokenPaths"/>: by whole path segments, ignoring case, against
        /// <c>HttpRequest.Path</c>. Each must start with <c>/</c>, must not end with one, and must not lie inside
        /// another listed prefix or repeat one in any case, since the browser would then send two cookies of the
        /// same name there and a repeated cookie is ignored. The cookie is read only when the request carries no
        /// <c>Authorization</c> header and the query string supplied no token, and never on a WebSocket or other
        /// protocol upgrade.
        /// </para>
        /// </summary>
        public IReadOnlyList<string> CookieTokenPaths { get; set; } = [];

        /// <summary>
        /// Validates the options and throws if any values are invalid.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="ClockSkew"/> is negative.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="Issuer"/> or <see cref="Audience"/> is empty or whitespace, or when a
        /// <see cref="QueryStringTokenPaths"/> or <see cref="CookieTokenPaths"/> entry would never match a request
        /// path, or when a <see cref="CookieTokenPaths"/> entry lies inside another.
        /// </exception>
        internal void Validate()
        {
            if (ClockSkew < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ClockSkew), "Must be non-negative.");
            }

            if (Issuer is not null && string.IsNullOrWhiteSpace(Issuer))
            {
                throw new ArgumentException("Issuer must be null or a non-empty, non-whitespace string.", nameof(Issuer));
            }

            if (Audience is not null && string.IsNullOrWhiteSpace(Audience))
            {
                throw new ArgumentException("Audience must be null or a non-empty, non-whitespace string.", nameof(Audience));
            }

            ValidatePathPrefixes(QueryStringTokenPaths, nameof(QueryStringTokenPaths), "query-string token path", "/ws");
            ValidatePathPrefixes(CookieTokenPaths, nameof(CookieTokenPaths), "cookie token path", "/files");
            RejectNestedCookieTokenPaths();
        }

        /// <summary>
        /// Rejects a <see cref="CookieTokenPaths"/> entry that lies inside another, since a request under both
        /// carries both cookies and the reader ignores a repeated cookie, so the inner prefix could never authenticate.
        /// Applies to the cookie only: a query-string path writes no cookie.
        /// </summary>
        private void RejectNestedCookieTokenPaths()
        {
            if (new PathPrefixList(CookieTokenPaths).TryFindNestedPair(out PathString inner, out PathString outer))
            {
                throw new ArgumentException(
                    $"Cookie token path \"{inner.Value}\" lies inside \"{outer.Value}\". " +
                    "A request under both would carry two cookies of the same name, and a repeated cookie is ignored, so list only the outer prefix.",
                    nameof(CookieTokenPaths));
            }
        }

        /// <summary>
        /// Rejects a prefix that <see cref="PathString.StartsWithSegments(PathString)"/> would never match against a
        /// longer request path.
        /// </summary>
        private static void ValidatePathPrefixes(IReadOnlyList<string> paths, string optionName, string description, string example)
        {
            foreach (string path in paths)
            {
                // PathString.StartsWithSegments never matches a prefix ending in '/' against a longer path
                // ("/ws/" against "/ws/city"), and "/" only matches the root, so each of these would
                // silently never apply. Failing here turns that into a startup error.
                if (!path.StartsWith('/') || path.EndsWith('/'))
                {
                    throw new ArgumentException(
                        $"Each {description} must start with '/' and must not end with '/', such as \"{example}\". Got: \"{path}\".",
                        optionName);
                }
            }
        }
    }
}
