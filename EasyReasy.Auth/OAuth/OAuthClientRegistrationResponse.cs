using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The registration endpoint's successful response (RFC 7591 section 3.2.1): the registered metadata, which is what
    /// this server supports whatever was asked. Null members are left out.
    /// </summary>
    internal sealed class OAuthClientRegistrationResponse
    {
        /// <summary>
        /// The new client's id.
        /// </summary>
        [JsonPropertyName("client_id")]
        public required string ClientId { get; init; }

        /// <summary>
        /// When the id was issued, in seconds since the Unix epoch.
        /// </summary>
        [JsonPropertyName("client_id_issued_at")]
        public required long ClientIdIssuedAt { get; init; }

        /// <summary>
        /// The client secret, for a confidential client; null for a public one.
        /// </summary>
        [JsonPropertyName("client_secret")]
        public string? ClientSecret { get; init; }

        /// <summary>
        /// 0 (the secret never expires) when there is a secret; null otherwise.
        /// </summary>
        [JsonPropertyName("client_secret_expires_at")]
        public long? ClientSecretExpiresAt => ClientSecret == null ? null : 0;

        /// <summary>
        /// The self-asserted name, or null.
        /// </summary>
        [JsonPropertyName("client_name")]
        public string? ClientName { get; init; }

        /// <summary>
        /// The self-asserted home page, or null.
        /// </summary>
        [JsonPropertyName("client_uri")]
        public string? ClientUri { get; init; }

        /// <summary>
        /// The registered redirect URIs.
        /// </summary>
        [JsonPropertyName("redirect_uris")]
        public required IReadOnlyList<string> RedirectUris { get; init; }

        /// <summary>
        /// The grant types the client may use: the two this server supports.
        /// </summary>
        [JsonPropertyName("grant_types")]
        public IReadOnlyList<string> GrantTypes => OAuthMetadataDocuments.GrantTypesSupported;

        /// <summary>
        /// The response types the client may use: <c>code</c>.
        /// </summary>
        [JsonPropertyName("response_types")]
        public IReadOnlyList<string> ResponseTypes => OAuthMetadataDocuments.ResponseTypesSupported;

        /// <summary>
        /// How the client authenticates at the token and revocation endpoints.
        /// </summary>
        [JsonPropertyName("token_endpoint_auth_method")]
        public required string TokenEndpointAuthMethod { get; init; }
    }
}
