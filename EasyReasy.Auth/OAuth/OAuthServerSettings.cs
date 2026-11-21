namespace EasyReasy.Auth
{
    /// <summary>
    /// <see cref="OAuthServerOptions"/> validated once at registration, with every URL the server publishes worked out.
    /// </summary>
    internal sealed class OAuthServerSettings
    {
        private readonly string _resourceOrigin;
        private readonly string _resourcePath;

        private OAuthServerSettings(OAuthServerOptions options, Uri issuer, Uri resource)
        {
            Issuer = OAuthUrl.Origin(issuer);
            _resourceOrigin = OAuthUrl.Origin(resource);
            _resourcePath = OAuthUrl.PathWithoutTrailingSlash(resource);
            Resource = _resourceOrigin + _resourcePath;
            ProtectedResourceMetadataPath = OAuthServerPaths.ProtectedResourceMetadataPrefix + _resourcePath;
            ProtectedResourceMetadataUrl = Issuer + ProtectedResourceMetadataPath;
            ConsentPageUrl = options.ConsentPageUrl;
            ScopesSupported = options.ScopesSupported.ToArray();
            AccessTokenLifetime = options.AccessTokenLifetime;
            RefreshTokenLifetime = options.RefreshTokenLifetime;
            AuthorizationCodeLifetime = options.AuthorizationCodeLifetime;
            AuthorizationRequestLifetime = options.AuthorizationRequestLifetime;
            AllowDynamicClientRegistration = options.AllowDynamicClientRegistration;
            AllowClientIdMetadataDocuments = options.AllowClientIdMetadataDocuments;
        }

        /// <summary>
        /// The issuer: the configured origin, lowercased, with no trailing slash.
        /// </summary>
        public string Issuer { get; }

        /// <summary>
        /// The protected resource identifier, lowercased origin and configured path, with no trailing slash.
        /// </summary>
        public string Resource { get; }

        /// <summary>
        /// The path the protected resource metadata is served at.
        /// </summary>
        public string ProtectedResourceMetadataPath { get; }

        /// <summary>
        /// The absolute URL of the protected resource metadata, as the 401 challenge names it.
        /// </summary>
        public string ProtectedResourceMetadataUrl { get; }

        /// <summary>
        /// The application's consent page.
        /// </summary>
        public string ConsentPageUrl { get; }

        /// <summary>
        /// The scopes the metadata lists.
        /// </summary>
        public IReadOnlyList<string> ScopesSupported { get; }

        /// <summary>
        /// How long an access token is valid.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; }

        /// <summary>
        /// How long a refresh token is valid.
        /// </summary>
        public TimeSpan RefreshTokenLifetime { get; }

        /// <summary>
        /// How long an authorization code can be redeemed.
        /// </summary>
        public TimeSpan AuthorizationCodeLifetime { get; }

        /// <summary>
        /// How long a pending authorization request can be completed.
        /// </summary>
        public TimeSpan AuthorizationRequestLifetime { get; }

        /// <summary>
        /// Whether dynamic client registration is on.
        /// </summary>
        public bool AllowDynamicClientRegistration { get; }

        /// <summary>
        /// Whether client ID metadata documents are accepted.
        /// </summary>
        public bool AllowClientIdMetadataDocuments { get; }

        /// <summary>
        /// The absolute URL of an endpoint path under the issuer.
        /// </summary>
        /// <param name="path">A path from <see cref="OAuthServerPaths"/>.</param>
        /// <returns>The absolute URL.</returns>
        public string EndpointUrl(string path)
        {
            return Issuer + path;
        }

        /// <summary>
        /// Whether a client's <c>resource</c> parameter names this server's resource: the same origin (scheme and host
        /// compared without case, as RFC 8707 asks a server to accept) and the resource's path or a path under it, with
        /// no query or fragment.
        /// </summary>
        /// <param name="requested">The parameter's value.</param>
        /// <returns>Whether the resource is this server's.</returns>
        public bool IsOwnResource(string requested)
        {
            if (OAuthUrl.HasFragment(requested)
                || !Uri.TryCreate(requested, UriKind.Absolute, out Uri? uri)
                || !string.IsNullOrEmpty(uri.Query)
                || OAuthUrl.Origin(uri) != _resourceOrigin)
            {
                return false;
            }

            string path = OAuthUrl.PathWithoutTrailingSlash(uri);
            return path == _resourcePath || path.StartsWith(_resourcePath + "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// Validates the options, reporting every problem in one exception.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="ArgumentException">The options are invalid.</exception>
        public static OAuthServerSettings Create(OAuthServerOptions options)
        {
            List<string> errors = new List<string>();

            Uri? issuer = null;
            if (!OAuthUrl.TryParseSecureContextUrl(options.IssuerUrl, out issuer)
                || OAuthUrl.PathWithoutTrailingSlash(issuer).Length > 0
                || !string.IsNullOrEmpty(issuer.Query)
                || OAuthUrl.HasFragment(options.IssuerUrl))
            {
                errors.Add($"{nameof(OAuthServerOptions.IssuerUrl)} '{options.IssuerUrl}' must be an absolute https URL (or http on a loopback host) with no path, query or fragment.");
                issuer = null;
            }

            Uri? resource = null;
            if (!OAuthUrl.TryParseSecureContextUrl(options.Resource, out resource)
                || !string.IsNullOrEmpty(resource.Query)
                || OAuthUrl.HasFragment(options.Resource))
            {
                errors.Add($"{nameof(OAuthServerOptions.Resource)} '{options.Resource}' must be an absolute https URL (or http on a loopback host) with no query or fragment.");
                resource = null;
            }
            else if (issuer != null && OAuthUrl.Origin(resource) != OAuthUrl.Origin(issuer))
            {
                errors.Add($"{nameof(OAuthServerOptions.Resource)} '{options.Resource}' must be on the same origin as {nameof(OAuthServerOptions.IssuerUrl)} '{options.IssuerUrl}': the tokens this server issues are only accepted by this server.");
                resource = null;
            }

            if (!IsAllowedConsentPageUrl(options.ConsentPageUrl))
            {
                errors.Add($"{nameof(OAuthServerOptions.ConsentPageUrl)} '{options.ConsentPageUrl}' must be an absolute https URL (or http on a loopback host) or a path starting with '/', without a fragment.");
            }

            if (options.ScopesSupported == null || options.ScopesSupported.Any(scope => string.IsNullOrEmpty(scope) || scope.Any(character => character <= ' ' || character == '"' || character == '\\' || character > '~')))
            {
                errors.Add($"{nameof(OAuthServerOptions.ScopesSupported)} must hold only non-empty scope tokens of printable ASCII without spaces, quotes or backslashes (RFC 6749 section 3.3).");
            }

            AddIfNotPositive(errors, options.AccessTokenLifetime, nameof(OAuthServerOptions.AccessTokenLifetime));
            AddIfNotPositive(errors, options.RefreshTokenLifetime, nameof(OAuthServerOptions.RefreshTokenLifetime));
            AddIfNotPositive(errors, options.AuthorizationCodeLifetime, nameof(OAuthServerOptions.AuthorizationCodeLifetime));
            AddIfNotPositive(errors, options.AuthorizationRequestLifetime, nameof(OAuthServerOptions.AuthorizationRequestLifetime));

            if (errors.Count > 0 || issuer == null || resource == null)
            {
                throw new ArgumentException($"Invalid {nameof(OAuthServerOptions)}:\n{string.Join("\n", errors)}", nameof(options));
            }

            return new OAuthServerSettings(options, issuer, resource);
        }

        private static bool IsAllowedConsentPageUrl(string? consentPageUrl)
        {
            if (string.IsNullOrEmpty(consentPageUrl) || OAuthUrl.HasFragment(consentPageUrl))
            {
                return false;
            }

            // "//host/path" is a scheme-relative URL to another host, not a path.
            if (consentPageUrl.StartsWith('/'))
            {
                return !consentPageUrl.StartsWith("//", StringComparison.Ordinal) && !consentPageUrl.StartsWith("/\\", StringComparison.Ordinal);
            }

            return OAuthUrl.TryParseSecureContextUrl(consentPageUrl, out _);
        }

        private static void AddIfNotPositive(List<string> errors, TimeSpan value, string name)
        {
            if (value <= TimeSpan.Zero)
            {
                errors.Add($"{name} must be positive; got {value}.");
            }
        }
    }
}
