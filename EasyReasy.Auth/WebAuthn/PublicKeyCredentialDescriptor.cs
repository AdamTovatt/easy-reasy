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
        /// How the browser can reach this credential's authenticator, as its registration reported it, or
        /// null to leave the field out — which is what a credential with no stored transports gets.
        /// </summary>
        /// <remarks>
        /// A hint only: the browser uses it to go straight to the authenticator it names rather than
        /// offering every kind, and the ceremony is verified the same way with or without it.
        /// </remarks>
        [JsonPropertyName("transports")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? Transports { get; }

        /// <summary>
        /// Initializes the descriptor from values already validated by the caller. Internal: the id has to
        /// be canonical base64url and the transports bounded before they get here, and
        /// <see cref="WebAuthnOptionsGenerator"/> is where that is checked so the failure names the argument
        /// the application passed.
        /// </summary>
        /// <param name="credentialId">The stored credential id, canonical base64url.</param>
        /// <param name="transports">The stored transports, non-empty, or null to omit the field.</param>
        internal PublicKeyCredentialDescriptor(string credentialId, IReadOnlyList<string>? transports)
        {
            Id = credentialId;
            Transports = transports;
        }
    }
}
