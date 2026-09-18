using System.Text;
using System.Text.Json;

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

        /// <summary>The ceremony the client data records.</summary>
        public string CeremonyType { get; set; } = CollectedClientData.RegistrationCeremonyType;

        /// <summary>The challenge the client data answers, base64url-encoded.</summary>
        public string Challenge { get; set; }

        /// <summary>The origin the client data reports the ceremony ran at.</summary>
        public string Origin { get; set; } = WebAuthnTestData.Origin;

        /// <summary>
        /// The <c>crossOrigin</c> the client data reports, or null to leave the field out entirely — which
        /// is the case a browser produces when the page is not framed at all.
        /// </summary>
        public bool? CrossOrigin { get; set; } = false;

        /// <summary>
        /// Client data JSON to send instead of one built from the fields above, for the cases where what is
        /// wrong with it is that it is not client data.
        /// </summary>
        public string? ClientDataJsonOverride { get; set; }

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

        /// <summary>The credential type the response reports.</summary>
        public string CredentialType { get; set; } = PublicKeyCredentialType.PublicKey;

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
            Challenge = challenge;

            AuthenticatorData = new AuthenticatorDataBuilder
            {
                RelyingPartyIdHash = WebAuthnTestData.RelyingPartyIdHash,
                Aaguid = authenticator.Aaguid,
                CredentialId = authenticator.CredentialId,
                CredentialPublicKey = authenticator.EncodeCoseKey(),
            };
        }

        /// <summary>
        /// Builds the collected client data JSON, as the browser would write it.
        /// </summary>
        private string BuildClientDataJson()
        {
            if (ClientDataJsonOverride != null)
            {
                return ClientDataJsonOverride;
            }

            Dictionary<string, object> fields = new Dictionary<string, object>
            {
                ["type"] = CeremonyType,
                ["challenge"] = Challenge,
                ["origin"] = Origin,
            };

            if (CrossOrigin != null)
            {
                fields["crossOrigin"] = CrossOrigin.Value;
            }

            return JsonSerializer.Serialize(fields);
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
                Base64UrlEncoding.Encode(Encoding.UTF8.GetBytes(BuildClientDataJson())),
                Base64UrlEncoding.Encode(BuildAttestationObject()),
                Transports);

            return new WebAuthnRegistrationResponse(credentialId, credentialId, CredentialType, response);
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
