using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The options a browser needs to run an authentication ceremony (WebAuthn Level 3 §5.5), in the JSON
    /// shape <c>PublicKeyCredential.parseRequestOptionsFromJSON()</c> consumes.
    /// </summary>
    /// <remarks>
    /// Travels one way, server to browser: produced by <see cref="WebAuthnOptionsGenerator"/>, consumed by a
    /// page, never parsed back, so it carries <see cref="ToJson"/> and no <c>FromJson</c>. What an
    /// application keeps across the round trip is <see cref="Challenge"/> and the user-verification
    /// requirement, both of which verification takes as arguments — and making the options deserializable
    /// would mean a constructor taking already-encoded base64url strings, which is the encoding this package
    /// owns on the application's behalf. See the remarks on <see cref="PublicKeyCredentialCreationOptions"/>.
    /// </remarks>
    public sealed class PublicKeyCredentialRequestOptions
    {
        /// <summary>
        /// The challenge this ceremony must answer, base64url-encoded. Store it against the session and
        /// hand it back to verification.
        /// </summary>
        [JsonPropertyName("challenge")]
        public string Challenge { get; }

        /// <summary>How long the browser should wait for the user, in milliseconds.</summary>
        [JsonPropertyName("timeout")]
        public int Timeout { get; }

        /// <summary>The relying-party id the credential is scoped to.</summary>
        [JsonPropertyName("rpId")]
        public string RelyingPartyId { get; }

        /// <summary>
        /// The credentials this user may answer with. Never empty: an empty list is how a relying party
        /// asks for any discoverable credential, which is passwordless login and out of scope here.
        /// </summary>
        [JsonPropertyName("allowCredentials")]
        public IReadOnlyList<PublicKeyCredentialDescriptor> AllowCredentials { get; }

        /// <summary>How strongly the authenticator is asked to verify the user.</summary>
        [JsonPropertyName("userVerification")]
        public WebAuthnUserVerificationRequirement UserVerification { get; }

        internal PublicKeyCredentialRequestOptions(
            string challenge,
            int timeout,
            string relyingPartyId,
            IReadOnlyList<PublicKeyCredentialDescriptor> allowCredentials,
            WebAuthnUserVerificationRequirement userVerification)
        {
            Challenge = challenge;
            Timeout = timeout;
            RelyingPartyId = relyingPartyId;
            AllowCredentials = allowCredentials;
            UserVerification = userVerification;
        }

        /// <summary>
        /// Serializes these options to the JSON a browser expects.
        /// </summary>
        /// <returns>A JSON string representation of these options.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns the JSON representation of these options.
        /// </summary>
        /// <returns>The same string as <see cref="ToJson"/>.</returns>
        public override string ToString()
        {
            return ToJson();
        }
    }
}
