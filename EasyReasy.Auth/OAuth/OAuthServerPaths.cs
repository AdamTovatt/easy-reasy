namespace EasyReasy.Auth
{
    /// <summary>
    /// The paths <see cref="OAuthServerApplicationBuilderExtensions.AddOAuthServerEndpoints"/> maps. The metadata documents
    /// list the client-facing ones as absolute URLs under <see cref="OAuthServerOptions.IssuerUrl"/>; the consent page
    /// calls the request ones.
    /// </summary>
    public static class OAuthServerPaths
    {
        /// <summary>
        /// The authorization server metadata (RFC 8414).
        /// </summary>
        public const string AuthorizationServerMetadata = "/.well-known/oauth-authorization-server";

        /// <summary>
        /// The prefix of the protected resource metadata (RFC 9728); the resource's own path follows it.
        /// </summary>
        public const string ProtectedResourceMetadataPrefix = "/.well-known/oauth-protected-resource";

        /// <summary>
        /// The authorization endpoint.
        /// </summary>
        public const string Authorize = "/oauth/authorize";

        /// <summary>
        /// The token endpoint.
        /// </summary>
        public const string Token = "/oauth/token";

        /// <summary>
        /// The dynamic client registration endpoint (RFC 7591).
        /// </summary>
        public const string Register = "/oauth/register";

        /// <summary>
        /// The token revocation endpoint (RFC 7009).
        /// </summary>
        public const string Revoke = "/oauth/revoke";

        /// <summary>
        /// The pending authorization requests the consent page reads and completes: <c>GET {prefix}/{request}</c> for what
        /// is being connected, and <c>POST {prefix}/{request}/approve</c> and <c>POST {prefix}/{request}/deny</c>.
        /// </summary>
        public const string AuthorizationRequestsPrefix = "/oauth/authorize/requests";
    }
}
