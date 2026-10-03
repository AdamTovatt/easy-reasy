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
        /// Validates the options and throws if any values are invalid.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="ClockSkew"/> is negative.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="Issuer"/> or <see cref="Audience"/> is empty or whitespace, or when a
        /// <see cref="QueryStringTokenPaths"/> entry would never match a request path.
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

            foreach (string path in QueryStringTokenPaths)
            {
                // PathString.StartsWithSegments never matches a prefix ending in '/' against a longer path
                // ("/ws/" against "/ws/city"), and "/" only matches the root, so each of these would
                // silently never apply. Failing here turns that into a startup error.
                if (!path.StartsWith('/') || path.EndsWith('/'))
                {
                    throw new ArgumentException(
                        $"Each query-string token path must start with '/' and must not end with '/', such as \"/ws\". Got: \"{path}\".",
                        nameof(QueryStringTokenPaths));
                }
            }
        }
    }
}
