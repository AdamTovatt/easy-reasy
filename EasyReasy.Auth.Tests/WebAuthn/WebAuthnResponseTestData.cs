namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The hand-written browser response the response-model tests are written against.
    /// </summary>
    /// <remarks>
    /// Deliberately not a verifiable ceremony: these tests are about the JSON contract, and the encoded
    /// fields only have to be base64url of something. A ceremony that also verifies is what
    /// <see cref="SyntheticRegistrationCeremony"/> builds, and the two meet in one test that parses its
    /// JSON and verifies the result.
    /// </remarks>
    internal static class WebAuthnResponseTestData
    {
        /// <summary>Base64url of <c>{"type":"webauthn.create"}</c>.</summary>
        public const string ClientDataJson = "eyJ0eXBlIjoid2ViYXV0aG4uY3JlYXRlIn0";

        /// <summary>Base64url of a short CBOR fragment — distinct from every other value here.</summary>
        public const string AttestationObject = "o2NmbXRkbm9uZQ";

        /// <summary>Base64url of "credential-one".</summary>
        public const string CredentialId = "Y3JlZGVudGlhbC1vbmU";

        /// <summary>Base64url of "credential-two", for showing two ids are not the same credential.</summary>
        public const string OtherCredentialId = "Y3JlZGVudGlhbC10d28";

        /// <summary>
        /// The body a page posts back, with every field the browser writes.
        /// </summary>
        public static string BrowserJson()
        {
            return $"{{\"{WebAuthnRegistrationResponse.IdFieldName}\":\"{CredentialId}\"," +
                $"\"{WebAuthnRegistrationResponse.RawIdFieldName}\":\"{CredentialId}\"," +
                $"\"{WebAuthnRegistrationResponse.TypeFieldName}\":\"{PublicKeyCredentialType.PublicKey}\"," +
                $"\"{WebAuthnRegistrationResponse.AuthenticatorAttachmentFieldName}\":\"platform\"," +
                "\"clientExtensionResults\":{}," +
                $"\"{WebAuthnRegistrationResponse.ResponseFieldName}\":{{" +
                $"\"{WebAuthnAttestationResponse.ClientDataJsonFieldName}\":\"{ClientDataJson}\"," +
                $"\"{WebAuthnAttestationResponse.AttestationObjectFieldName}\":\"{AttestationObject}\"," +
                $"\"{WebAuthnAttestationResponse.TransportsFieldName}\":[\"internal\",\"hybrid\"]}}}}";
        }
    }
}
