using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The relying party as the browser's creation options name it (WebAuthn Level 3 §5.4.2).
    /// </summary>
    public sealed class PublicKeyCredentialRpEntity
    {
        /// <summary>The relying-party id credentials are scoped to.</summary>
        [JsonPropertyName("id")]
        public string Id { get; }

        /// <summary>The human-readable name shown to the user.</summary>
        [JsonPropertyName("name")]
        public string Name { get; }

        /// <summary>
        /// Initializes the relying-party entity from a validated configuration. Internal: it carries
        /// nothing a caller chooses that <see cref="WebAuthnRelyingParty"/> has not already validated.
        /// </summary>
        /// <param name="relyingParty">The configuration to take the id and name from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="relyingParty"/> is null.</exception>
        internal PublicKeyCredentialRpEntity(WebAuthnRelyingParty relyingParty)
        {
            ArgumentNullException.ThrowIfNull(relyingParty);

            Id = relyingParty.Id;
            Name = relyingParty.Name;
        }
    }
}
