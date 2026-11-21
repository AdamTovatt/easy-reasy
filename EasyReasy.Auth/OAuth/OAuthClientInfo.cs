using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What the consent page and <see cref="IOAuthGrantHandler.ApproveAsync"/> know about the client asking to connect.
    /// Only <see cref="ClientId"/> and, for a metadata document, <see cref="MetadataDocumentHost"/> are facts; the name and
    /// URI are whatever the client said about itself.
    /// </summary>
    public sealed class OAuthClientInfo
    {
        /// <summary>
        /// The client id.
        /// </summary>
        [JsonPropertyName("clientId")]
        public required string ClientId { get; init; }

        /// <summary>
        /// How the server knows the client.
        /// </summary>
        [JsonPropertyName("registrationKind")]
        [JsonConverter(typeof(JsonStringEnumConverter<OAuthClientRegistrationKind>))]
        public required OAuthClientRegistrationKind RegistrationKind { get; init; }

        /// <summary>
        /// The client's self-asserted name, or null. Show it beside the approving person, never as who the client is.
        /// </summary>
        [JsonPropertyName("clientName")]
        public string? ClientName { get; init; }

        /// <summary>
        /// The client's self-asserted home page, or null.
        /// </summary>
        [JsonPropertyName("clientUri")]
        public string? ClientUri { get; init; }

        /// <summary>
        /// For a client identified by a metadata document, the host of its client id URL, which the client cannot assert
        /// falsely: whoever answers at that host published the document. Show it on the consent page (the metadata
        /// document draft asks for this). Null for a registered client.
        /// </summary>
        [JsonPropertyName("metadataDocumentHost")]
        public string? MetadataDocumentHost { get; init; }

        /// <summary>
        /// The one place a client's info is put together: the metadata document host is read from the client id URL, so
        /// it is never stored or passed apart from it.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="registrationKind">How the server knows the client.</param>
        /// <param name="clientName">The self-asserted name, or null.</param>
        /// <param name="clientUri">The self-asserted home page, or null.</param>
        /// <returns>The info.</returns>
        internal static OAuthClientInfo Create(string clientId, OAuthClientRegistrationKind registrationKind, string? clientName, string? clientUri)
        {
            string? host = null;
            if (registrationKind == OAuthClientRegistrationKind.MetadataDocument
                && Uri.TryCreate(clientId, UriKind.Absolute, out Uri? clientIdUrl))
            {
                host = clientIdUrl.Host;
            }

            return new OAuthClientInfo
            {
                ClientId = clientId,
                RegistrationKind = registrationKind,
                ClientName = clientName,
                ClientUri = clientUri,
                MetadataDocumentHost = host,
            };
        }
    }
}
