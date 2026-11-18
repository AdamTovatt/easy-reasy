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
    /// <para>
    /// It can be a property of a response model: its System.Text.Json converter writes exactly the bytes of
    /// <see cref="ToJson"/>, whatever options the host serializes with, and refuses to read with a
    /// <see cref="NotSupportedException"/>.
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(WebAuthnOptionsJsonConverter<PublicKeyCredentialRequestOptions>))]
    public sealed class PublicKeyCredentialRequestOptions : IWebAuthnJsonWritable
    {
        /// <summary>
        /// The challenge this ceremony must answer, base64url-encoded. Store it against the session and
        /// hand it back to verification.
        /// </summary>
        public string Challenge { get; }

        /// <summary>How long the browser should wait for the user, in milliseconds.</summary>
        public int Timeout { get; }

        /// <summary>The relying-party id the credential is scoped to.</summary>
        public string RelyingPartyId { get; }

        /// <summary>
        /// The credentials this user may answer with. Never empty for a second factor; empty exactly when
        /// <see cref="WebAuthnOptionsGenerator.CreateDiscoverableAuthenticationOptions"/> made these
        /// options, which is how a relying party asks for any discoverable credential (usernameless
        /// passkey sign-in).
        /// </summary>
        public IReadOnlyList<PublicKeyCredentialDescriptor> AllowCredentials { get; }

        /// <summary>How strongly the authenticator is asked to verify the user.</summary>
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
            return WebAuthnJson.ToJson(this);
        }

        void IWebAuthnJsonWritable.WriteJson(Utf8JsonWriter writer)
        {
            // The member names and order of PublicKeyCredentialRequestOptionsJSON (WebAuthn Level 3 §5.5).
            writer.WriteStartObject();
            writer.WriteString("challenge", Challenge);
            writer.WriteNumber("timeout", Timeout);
            writer.WriteString("rpId", RelyingPartyId);
            WebAuthnJson.WriteProperty(writer, "allowCredentials", AllowCredentials);
            WebAuthnJson.WriteProperty(writer, "userVerification", UserVerification);
            writer.WriteEndObject();
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
