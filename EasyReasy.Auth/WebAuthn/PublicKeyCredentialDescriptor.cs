using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A reference to one credential the browser should offer or refuse (WebAuthn Level 3 §5.8.3): the
    /// entries of <c>allowCredentials</c> at authentication and of <c>excludeCredentials</c> at registration.
    /// </summary>
    public sealed class PublicKeyCredentialDescriptor
    {
        /// <summary>The credential type, always <c>public-key</c>.</summary>
        [JsonPropertyName("type")]
        public string Type => PublicKeyCredentialType.PublicKey;

        /// <summary>
        /// The credential id, base64url-encoded — the same string a registration reported and the
        /// application stored, handed back unchanged.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; }

        /// <summary>
        /// Initializes the descriptor from a credential id already validated by the caller. Internal: the
        /// id has to be canonical base64url before it gets here, and <see cref="WebAuthnOptionsGenerator"/>
        /// is where that is checked so the failure names the argument the application passed.
        /// </summary>
        /// <param name="credentialId">The stored credential id, canonical base64url.</param>
        internal PublicKeyCredentialDescriptor(string credentialId)
        {
            Id = credentialId;
        }
    }
}
