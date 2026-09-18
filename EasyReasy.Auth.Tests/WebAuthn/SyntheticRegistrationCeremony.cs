using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Assembles the registration a browser would post back, from a <see cref="SyntheticAuthenticator"/>
    /// that holds a real key pair. Every field a verification check reads is a property here, defaulted to
    /// the value that passes, so a test breaks exactly one and shows that exactly that check fails.
    /// </summary>
    /// <remarks>
    /// The point of building the ceremony rather than pasting a captured one is that a captured response is
    /// correct in every field at once: nothing can be mutated without re-deriving the rest of it, so the
    /// checks would all be exercised by the same passing case and none of them proven.
    /// </remarks>
    internal sealed class SyntheticRegistrationCeremony
    {
        private readonly SyntheticAuthenticator _authenticator;

        /// <summary>The client data the browser collected, field by field.</summary>
        public ClientDataBuilder ClientData { get; }

        /// <summary>The authenticator data to place in the attestation object, field by field.</summary>
        public AuthenticatorDataBuilder AuthenticatorData { get; }

        /// <summary>The attestation statement format the attestation object states.</summary>
        public string? AttestationFormat { get; set; } = AttestationObject.NoneFormat;

        /// <summary>
        /// Attestation object bytes to send instead of one built from the fields above, for the cases where
        /// what is wrong with it is that it is not CBOR.
        /// </summary>
        public byte[]? AttestationObjectOverride { get; set; }

        /// <summary>
        /// The credential id the response reports in its <c>id</c> and <c>rawId</c>, base64url-encoded, or
        /// null to derive it from the credential id in the authenticator data — which is what a browser
        /// does, the two being the same bytes.
        /// </summary>
        public string? ReportedCredentialId { get; set; }

        /// <summary>The transports the browser reports, or null when it reports none.</summary>
        public IEnumerable<string>? Transports { get; set; }

        /// <summary>
        /// Creates a ceremony that verifies against <see cref="WebAuthnTestData"/>'s relying party, using
        /// the given authenticator and answering the given challenge.
        /// </summary>
        /// <param name="authenticator">The authenticator whose key and credential id the ceremony enrolls.</param>
        /// <param name="challenge">The base64url challenge the ceremony answers.</param>
        public SyntheticRegistrationCeremony(SyntheticAuthenticator authenticator, string challenge)
        {
            _authenticator = authenticator;
            ClientData = new ClientDataBuilder(CollectedClientData.RegistrationCeremonyType, challenge);

            AuthenticatorData = new AuthenticatorDataBuilder
            {
                RelyingPartyIdHash = WebAuthnTestData.RelyingPartyIdHash,
                Aaguid = authenticator.Aaguid,
                CredentialId = authenticator.CredentialId,
                CredentialPublicKey = authenticator.EncodeCoseKey(),
            };
        }

        /// <summary>
        /// Builds the CBOR attestation object. Always with the empty attestation statement the <c>none</c>
        /// format is defined as — a statement carrying anything is rejected a layer below this, where
        /// <c>AttestationObjectTests</c> covers it against the parser directly.
        /// </summary>
        private byte[] BuildAttestationObject()
        {
            if (AttestationObjectOverride != null)
            {
                return AttestationObjectOverride;
            }

            return CborTestEncoder.EncodeAttestationObject(
                AttestationFormat,
                AuthenticatorData.Build(),
                CborTestEncoder.EncodeEmptyMap());
        }

        /// <summary>
        /// Builds the registration response object verification is handed.
        /// </summary>
        public WebAuthnRegistrationResponse Build()
        {
            string credentialId = ReportedCredentialId ?? Base64UrlEncoding.Encode(AuthenticatorData.CredentialId ?? _authenticator.CredentialId);

            WebAuthnAttestationResponse response = new WebAuthnAttestationResponse(
                Base64UrlEncoding.Encode(Encoding.UTF8.GetBytes(ClientData.Build())),
                Base64UrlEncoding.Encode(BuildAttestationObject()),
                Transports);

            return new WebAuthnRegistrationResponse(credentialId, credentialId, PublicKeyCredentialType.PublicKey, response);
        }

        /// <summary>
        /// Builds the JSON body a page posts back, as the browser's <c>toJSON()</c> writes it.
        /// </summary>
        public string BuildJson()
        {
            return Build().ToJson();
        }
    }
}
