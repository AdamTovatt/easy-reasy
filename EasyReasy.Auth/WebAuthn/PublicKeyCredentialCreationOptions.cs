using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The options a browser needs to run a registration ceremony (WebAuthn Level 3 §5.4), in the JSON
    /// shape <c>PublicKeyCredential.parseCreationOptionsFromJSON()</c> consumes.
    /// </summary>
    /// <remarks>
    /// This type travels one way, server to browser: it is produced by <see cref="WebAuthnOptionsGenerator"/>
    /// and consumed by a page, and nothing parses it back. It therefore carries <see cref="ToJson"/> without
    /// the matching <c>FromJson</c> the repository's request and response models have, for two reasons.
    /// What an application has to keep across the round trip is <see cref="Challenge"/> and the
    /// user-verification requirement it asked for, both of which verification takes as arguments, so storing
    /// the whole object and re-reading it would be a longer way to the same two values. And a deserializable
    /// type needs a constructor whose parameters match its serialized properties, which would mean taking
    /// the user handle as an already-encoded base64url string — exporting to every caller the base64url
    /// question this package exists to answer for them.
    /// <para>
    /// It can be a property of a response model: its System.Text.Json converter writes exactly the bytes of
    /// <see cref="ToJson"/>, whatever options the host serializes with, and refuses to read with a
    /// <see cref="NotSupportedException"/>.
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(WebAuthnOptionsJsonConverter<PublicKeyCredentialCreationOptions>))]
    public sealed class PublicKeyCredentialCreationOptions : IWebAuthnJsonWritable
    {
        /// <summary>The relying party the credential will be scoped to.</summary>
        public PublicKeyCredentialRpEntity RelyingParty { get; }

        /// <summary>The user the credential is being registered for.</summary>
        public PublicKeyCredentialUserEntity User { get; }

        /// <summary>
        /// The challenge this ceremony must answer, base64url-encoded. Store it against the session and
        /// hand it back to verification; this library generates it and nothing more.
        /// </summary>
        public string Challenge { get; }

        /// <summary>The algorithms the relying party accepts, most preferred first.</summary>
        public IReadOnlyList<PublicKeyCredentialParameters> AcceptedAlgorithms { get; }

        /// <summary>How long the browser should wait for the user, in milliseconds.</summary>
        public int Timeout { get; }

        /// <summary>
        /// Credentials the user has already registered, so the browser refuses to enroll the same
        /// authenticator twice. Omitted from the JSON when empty.
        /// </summary>
        public IReadOnlyList<PublicKeyCredentialDescriptor>? ExcludeCredentials { get; }

        /// <summary>What the relying party asks of the authenticator.</summary>
        public AuthenticatorSelectionCriteria AuthenticatorSelection { get; }

        /// <summary>
        /// Always <c>none</c>. Verifying an attestation statement answers
        /// "what kind of authenticator is this", which only matters under an authenticator-allowlist policy
        /// this library does not implement — and asking for one the library will not verify would collect a
        /// statement nothing reads.
        /// </summary>
        public string Attestation => AttestationObject.NoneFormat;

        internal PublicKeyCredentialCreationOptions(
            PublicKeyCredentialRpEntity relyingParty,
            PublicKeyCredentialUserEntity user,
            string challenge,
            IReadOnlyList<PublicKeyCredentialParameters> acceptedAlgorithms,
            int timeout,
            IReadOnlyList<PublicKeyCredentialDescriptor>? excludeCredentials,
            AuthenticatorSelectionCriteria authenticatorSelection)
        {
            RelyingParty = relyingParty;
            User = user;
            Challenge = challenge;
            AcceptedAlgorithms = acceptedAlgorithms;
            Timeout = timeout;
            ExcludeCredentials = excludeCredentials;
            AuthenticatorSelection = authenticatorSelection;
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
            // The member names and order of PublicKeyCredentialCreationOptionsJSON (WebAuthn Level 3 §5.4).
            writer.WriteStartObject();
            WebAuthnJson.WriteProperty(writer, "rp", RelyingParty);
            WebAuthnJson.WriteProperty(writer, "user", User);
            writer.WriteString("challenge", Challenge);
            WebAuthnJson.WriteProperty(writer, "pubKeyCredParams", AcceptedAlgorithms);
            writer.WriteNumber("timeout", Timeout);

            if (ExcludeCredentials != null)
            {
                WebAuthnJson.WriteProperty(writer, "excludeCredentials", ExcludeCredentials);
            }

            WebAuthnJson.WriteProperty(writer, "authenticatorSelection", AuthenticatorSelection);
            writer.WriteString("attestation", Attestation);
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
