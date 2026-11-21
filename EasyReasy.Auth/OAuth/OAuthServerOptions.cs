namespace EasyReasy.Auth
{
    /// <summary>
    /// Options for the OAuth 2.1 authorization server, passed to
    /// <see cref="OAuthServerServiceCollectionExtensions.AddEasyReasyOAuthServer{TStore, THandler}"/> and validated there.
    /// A record, so a variant (per environment, or in a test) is written as a copy <c>with</c> what differs.
    /// </summary>
    public sealed record OAuthServerOptions
    {
        /// <summary>
        /// The authorization server's issuer identifier (RFC 8414): the origin the application is served at, such as
        /// <c>https://app.example</c>. It must be an absolute https URL (http only on a loopback host) with no path,
        /// query or fragment. It is the <c>issuer</c> in the metadata, the base of every endpoint URL the metadata
        /// lists, and the <c>iss</c> on every authorization response (RFC 9207). It is configured rather than read from
        /// the request, so a forged Host header cannot change it.
        /// </summary>
        public required string IssuerUrl { get; init; }

        /// <summary>
        /// The protected resource's identifier (RFC 9728): the URL a client connects to, such as
        /// <c>https://app.example/mcp</c>, or the origin when the whole application is the resource. It must be an
        /// absolute URL on the same origin as <see cref="IssuerUrl"/>, without a query or fragment. Its metadata is
        /// served at <c>/.well-known/oauth-protected-resource</c> followed by its path, the JWT bearer's 401 points
        /// there, and a <c>resource</c> parameter must name it or a path under it.
        /// </summary>
        public required string Resource { get; init; }

        /// <summary>
        /// The application's consent page, where the authorization endpoint sends the browser with the pending request
        /// in a <c>request</c> query parameter. The page signs the person in with the application's own sign-in, shows
        /// what is being connected, and approves or denies through the request endpoints. An absolute https URL (http
        /// only on a loopback host) or a path starting with <c>/</c>.
        /// </summary>
        public required string ConsentPageUrl { get; init; }

        /// <summary>
        /// The scopes listed as <c>scopes_supported</c> in both metadata documents and in the 401 challenge's
        /// <c>scope</c>. Empty (the default) lists none. The server does not enforce scopes: the requested scope goes to
        /// <see cref="IOAuthGrantHandler.ApproveAsync"/>, which puts whatever it grants into the token's claims.
        /// </summary>
        public IReadOnlyList<string> ScopesSupported { get; init; } = [];

        /// <summary>
        /// How long an access token is valid. Default 10 minutes. An access token cannot be revoked, so this is how long
        /// a revoked grant's last token keeps working.
        /// </summary>
        public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// How long a refresh token is valid. Default 30 days. Each refresh issues a new one with a full lifetime, so a
        /// connection lasts while it is used and lapses after this long unused.
        /// </summary>
        public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);

        /// <summary>
        /// How long an authorization code can be redeemed. Default 1 minute; a client redeems it on the redirect.
        /// </summary>
        public TimeSpan AuthorizationCodeLifetime { get; init; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// How long a pending authorization request can be approved or denied. Default 10 minutes, time for the person
        /// to sign in, second factor included.
        /// </summary>
        public TimeSpan AuthorizationRequestLifetime { get; init; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Whether clients can register themselves at the registration endpoint (RFC 7591). Default true. When false,
        /// the endpoint is not mapped and the metadata lists no <c>registration_endpoint</c>.
        /// </summary>
        public bool AllowDynamicClientRegistration { get; init; } = true;

        /// <summary>
        /// Whether a client can identify itself by the https URL of a client ID metadata document, which the server
        /// fetches. Default true. When false, such a client id is unknown and the metadata says
        /// <c>client_id_metadata_document_supported: false</c>.
        /// </summary>
        public bool AllowClientIdMetadataDocuments { get; init; } = true;
    }
}
