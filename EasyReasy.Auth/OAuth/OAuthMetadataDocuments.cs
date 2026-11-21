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
        /// The authorization server metadata.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <returns>The JSON response.</returns>
        public static IResult AuthorizationServer(OAuthServerSettings settings)
        {
            Dictionary<string, object> document = new Dictionary<string, object>
            {
                ["issuer"] = settings.Issuer,
                ["authorization_endpoint"] = settings.EndpointUrl(OAuthServerPaths.Authorize),
                ["token_endpoint"] = settings.EndpointUrl(OAuthServerPaths.Token),
                ["revocation_endpoint"] = settings.EndpointUrl(OAuthServerPaths.Revoke),
                ["response_types_supported"] = new[] { "code" },
                ["response_modes_supported"] = new[] { "query" },
                ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
                ["code_challenge_methods_supported"] = new[] { "S256" },
                ["token_endpoint_auth_methods_supported"] = OAuthClientAuthenticationMethodNames.All,
                ["revocation_endpoint_auth_methods_supported"] = OAuthClientAuthenticationMethodNames.All,
                ["authorization_response_iss_parameter_supported"] = true,
                ["client_id_metadata_document_supported"] = settings.AllowClientIdMetadataDocuments,
            };

            if (settings.AllowDynamicClientRegistration)
            {
                document["registration_endpoint"] = settings.EndpointUrl(OAuthServerPaths.Register);
            }

            if (settings.ScopesSupported.Count > 0)
            {
                document["scopes_supported"] = settings.ScopesSupported;
            }

            return Results.Json(document, OAuthJson.Options);
        }

        /// <summary>
        /// The protected resource metadata.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <returns>The JSON response.</returns>
        public static IResult ProtectedResource(OAuthServerSettings settings)
        {
            Dictionary<string, object> document = new Dictionary<string, object>
            {
                ["resource"] = settings.Resource,
                ["authorization_servers"] = new[] { settings.Issuer },
                ["bearer_methods_supported"] = new[] { "header" },
            };

            if (settings.ScopesSupported.Count > 0)
            {
                document["scopes_supported"] = settings.ScopesSupported;
            }

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
    }
}
