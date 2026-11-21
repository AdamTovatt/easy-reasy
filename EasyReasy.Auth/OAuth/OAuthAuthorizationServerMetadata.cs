using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The authorization server metadata (RFC 8414). Null members are left out.
    /// </summary>
    internal sealed class OAuthAuthorizationServerMetadata
    {
        /// <summary>
        /// The issuer.
        /// </summary>
        [JsonPropertyName("issuer")]
        public required string Issuer { get; init; }

        /// <summary>
        /// The authorization endpoint's URL.
        /// </summary>
        [JsonPropertyName("authorization_endpoint")]
        public required string AuthorizationEndpoint { get; init; }

        /// <summary>
        /// The token endpoint's URL.
        /// </summary>
        [JsonPropertyName("token_endpoint")]
        public required string TokenEndpoint { get; init; }

        /// <summary>
        /// The revocation endpoint's URL.
        /// </summary>
        [JsonPropertyName("revocation_endpoint")]
        public required string RevocationEndpoint { get; init; }

        /// <summary>
        /// The registration endpoint's URL, or null when dynamic client registration is off.
        /// </summary>
        [JsonPropertyName("registration_endpoint")]
        public string? RegistrationEndpoint { get; init; }

        /// <summary>
        /// The supported response types: <c>code</c>.
        /// </summary>
        [JsonPropertyName("response_types_supported")]
        public IReadOnlyList<string> ResponseTypesSupported => OAuthMetadataDocuments.ResponseTypesSupported;

        /// <summary>
        /// The supported response modes: <c>query</c>.
        /// </summary>
        [JsonPropertyName("response_modes_supported")]
        public IReadOnlyList<string> ResponseModesSupported => ["query"];

        /// <summary>
        /// The supported grant types.
        /// </summary>
        [JsonPropertyName("grant_types_supported")]
        public IReadOnlyList<string> GrantTypesSupported => OAuthMetadataDocuments.GrantTypesSupported;

        /// <summary>
        /// The supported PKCE methods: <c>S256</c>.
        /// </summary>
        [JsonPropertyName("code_challenge_methods_supported")]
        public IReadOnlyList<string> CodeChallengeMethodsSupported => ["S256"];

        /// <summary>
        /// The client authentication methods the token endpoint accepts.
        /// </summary>
        [JsonPropertyName("token_endpoint_auth_methods_supported")]
        public IReadOnlyList<string> TokenEndpointAuthMethodsSupported => OAuthClientAuthenticationMethodNames.All;

        /// <summary>
        /// The client authentication methods the revocation endpoint accepts.
        /// </summary>
        [JsonPropertyName("revocation_endpoint_auth_methods_supported")]
        public IReadOnlyList<string> RevocationEndpointAuthMethodsSupported => OAuthClientAuthenticationMethodNames.All;

        /// <summary>
        /// Whether the authorization response carries <c>iss</c> (RFC 9207): always.
        /// </summary>
        [JsonPropertyName("authorization_response_iss_parameter_supported")]
        public bool AuthorizationResponseIssParameterSupported => true;

        /// <summary>
        /// Whether client ID metadata documents are accepted.
        /// </summary>
        [JsonPropertyName("client_id_metadata_document_supported")]
        public required bool ClientIdMetadataDocumentSupported { get; init; }

        /// <summary>
        /// The scopes the server names, or null when it names none.
        /// </summary>
        [JsonPropertyName("scopes_supported")]
        public IReadOnlyList<string>? ScopesSupported { get; init; }
    }
}
