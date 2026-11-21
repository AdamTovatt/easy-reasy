using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The two discovery documents an MCP client reads before anything else: the protected resource metadata (RFC 9728),
    /// which names this server as the resource's authorization server, and the authorization server metadata (RFC 8414).
    /// </summary>
    internal static class OAuthMetadataDocuments
    {
        /// <summary>
        /// The response types the server supports: <c>code</c>.
        /// </summary>
        public static readonly IReadOnlyList<string> ResponseTypesSupported = ["code"];

        /// <summary>
        /// The grant types the server supports.
        /// </summary>
        public static readonly IReadOnlyList<string> GrantTypesSupported = ["authorization_code", "refresh_token"];

        /// <summary>
        /// The authorization server metadata.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <returns>The JSON response.</returns>
        public static IResult AuthorizationServer(OAuthServerSettings settings)
        {
            OAuthAuthorizationServerMetadata document = new OAuthAuthorizationServerMetadata
            {
                Issuer = settings.Issuer,
                AuthorizationEndpoint = settings.EndpointUrl(OAuthServerPaths.Authorize),
                TokenEndpoint = settings.EndpointUrl(OAuthServerPaths.Token),
                RevocationEndpoint = settings.EndpointUrl(OAuthServerPaths.Revoke),
                RegistrationEndpoint = settings.AllowDynamicClientRegistration ? settings.EndpointUrl(OAuthServerPaths.Register) : null,
                ClientIdMetadataDocumentSupported = settings.AllowClientIdMetadataDocuments,
                ScopesSupported = ScopesOrNull(settings),
            };
            return Results.Json(document, OAuthJson.Options);
        }

        /// <summary>
        /// The protected resource metadata.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <returns>The JSON response.</returns>
        public static IResult ProtectedResource(OAuthServerSettings settings)
        {
            OAuthProtectedResourceMetadata document = new OAuthProtectedResourceMetadata
            {
                Resource = settings.Resource,
                AuthorizationServers = [settings.Issuer],
                ScopesSupported = ScopesOrNull(settings),
            };
            return Results.Json(document, OAuthJson.Options);
        }

        /// <summary>
        /// The JWT bearer's 401 challenge, pointing a client at the protected resource metadata (RFC 9728 section 5.1).
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <returns>The challenge, to set as <c>JwtBearerOptions.Challenge</c>.</returns>
        public static string BearerChallenge(OAuthServerSettings settings)
        {
            string challenge = $"Bearer resource_metadata=\"{settings.ProtectedResourceMetadataUrl}\"";
            if (settings.ScopesSupported.Count > 0)
            {
                challenge += $", scope=\"{string.Join(' ', settings.ScopesSupported)}\"";
            }

            return challenge;
        }

        // A server that names no scopes leaves scopes_supported out rather than writing an empty list.
        private static IReadOnlyList<string>? ScopesOrNull(OAuthServerSettings settings)
        {
            return settings.ScopesSupported.Count > 0 ? settings.ScopesSupported : null;
        }
    }
}
