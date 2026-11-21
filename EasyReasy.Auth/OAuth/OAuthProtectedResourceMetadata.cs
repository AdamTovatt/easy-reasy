using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The protected resource metadata (RFC 9728). Null members are left out.
    /// </summary>
    internal sealed class OAuthProtectedResourceMetadata
    {
        /// <summary>
        /// The resource identifier.
        /// </summary>
        [JsonPropertyName("resource")]
        public required string Resource { get; init; }

        /// <summary>
        /// The authorization servers for the resource: this server's issuer.
        /// </summary>
        [JsonPropertyName("authorization_servers")]
        public required IReadOnlyList<string> AuthorizationServers { get; init; }

        /// <summary>
        /// How a bearer token is sent: <c>header</c>.
        /// </summary>
        [JsonPropertyName("bearer_methods_supported")]
        public IReadOnlyList<string> BearerMethodsSupported => ["header"];

        /// <summary>
        /// The scopes the server names, or null when it names none.
        /// </summary>
        [JsonPropertyName("scopes_supported")]
        public IReadOnlyList<string>? ScopesSupported { get; init; }
    }
}
