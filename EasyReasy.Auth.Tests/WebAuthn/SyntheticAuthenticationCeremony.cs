using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Assembles the assertion a browser would post back, signed by a
    /// <see cref="SyntheticAuthenticator"/> holding a real private key. Every field a verification check
    /// reads is a property here, defaulted to the value that passes.
    /// </summary>
    /// <remarks>
    /// The signature is produced last, over whatever the other fields were left as, so breaking one of
    /// them produces an assertion that is correctly signed and wrong in exactly one place — which is what
    /// makes each check provable on its own rather than all of them collapsing into "the signature failed".
    /// <see cref="SignedBytesOverride"/> and <see cref="ClientDataJsonSentInstead"/> are for the tests that
    /// need the opposite: everything correct and the signature over something else.
    /// </remarks>
    internal sealed class SyntheticAuthenticationCeremony
    {
        private readonly SyntheticAuthenticator _authenticator;

        /// <summary>The client data the browser collected, field by field.</summary>
        public ClientDataBuilder ClientData { get; }

        /// <summary>
        /// The authenticator data to sign over. An assertion carries no attested credential data, so
        /// nothing here sets the credential fields — which is what makes it an assertion rather than an
        /// attestation.
        /// </summary>
        public AuthenticatorDataBuilder AuthenticatorData { get; }

        /// <summary>The credential id the response reports, base64url-encoded.</summary>
        public string? ReportedCredentialId { get; set; }

        /// <summary>The user handle the response reports, base64url-encoded, or null for none.</summary>
        public string? UserHandle { get; set; }

        /// <summary>
        /// Bytes to sign instead of the ones the assertion actually carries, for showing that a signature
        /// over anything else is rejected.
        /// </summary>
        public byte[]? SignedBytesOverride { get; set; }

        /// <summary>
        /// Client data to put in the response after the signature has been produced over the client data
        /// <see cref="ClientData"/> builds, for showing that the signature covers the bytes as received
        /// rather than a re-serialization of the fields read out of them.
        /// </summary>
        public string? ClientDataJsonSentInstead { get; set; }

        /// <summary>
        /// A signature to send instead of a real one.
        /// </summary>
        public byte[]? SignatureOverride { get; set; }

        /// <summary>
        /// Creates a ceremony answering the given challenge with the given authenticator's key.
        /// </summary>
        /// <param name="authenticator">The authenticator that signs the assertion.</param>
        /// <param name="challenge">The base64url challenge the ceremony answers.</param>
        public SyntheticAuthenticationCeremony(SyntheticAuthenticator authenticator, string challenge)
        {
            _authenticator = authenticator;
            ClientData = new ClientDataBuilder(CollectedClientData.AuthenticationCeremonyType, challenge);

            AuthenticatorData = new AuthenticatorDataBuilder
            {
                RelyingPartyIdHash = WebAuthnTestData.RelyingPartyIdHash,
            };
        }

        /// <summary>
        /// The credential this ceremony's authenticator registered, as the application would have stored it.
        /// </summary>
        /// <param name="signCount">The counter recorded against the credential.</param>
        public WebAuthnStoredCredential StoredCredential(uint signCount = 0)
        {
            return new WebAuthnStoredCredential(
                Base64UrlEncoding.Encode(_authenticator.CredentialId),
                Base64UrlEncoding.Encode(_authenticator.EncodeCoseKey()),
                signCount);
        }

        /// <summary>
        /// Builds the authentication response verification is handed.
        /// </summary>
        public WebAuthnAuthenticationResponse Build()
        {
            byte[] signedClientDataJsonBytes = Encoding.UTF8.GetBytes(ClientData.Build());
            byte[] authenticatorDataBytes = AuthenticatorData.Build();

            byte[] signedBytes = SignedBytesOverride ?? Concatenate(authenticatorDataBytes, SHA256.HashData(signedClientDataJsonBytes));
            byte[] signature = SignatureOverride ?? _authenticator.Sign(signedBytes);

            byte[] sentClientDataJsonBytes = ClientDataJsonSentInstead == null
                ? signedClientDataJsonBytes
                : Encoding.UTF8.GetBytes(ClientDataJsonSentInstead);

            string credentialId = ReportedCredentialId ?? Base64UrlEncoding.Encode(_authenticator.CredentialId);

            WebAuthnAssertionResponse response = new WebAuthnAssertionResponse(
                Base64UrlEncoding.Encode(sentClientDataJsonBytes),
                Base64UrlEncoding.Encode(authenticatorDataBytes),
                Base64UrlEncoding.Encode(signature),
                UserHandle);

            return new WebAuthnAuthenticationResponse(credentialId, credentialId, PublicKeyCredentialType.PublicKey, response);
        }

        /// <summary>
        /// Builds the JSON body a page posts back, as the browser's <c>toJSON()</c> writes it.
        /// </summary>
        public string BuildJson()
        {
            return Build().ToJson();
        }

        private static byte[] Concatenate(byte[] first, byte[] second)
        {
            byte[] combined = new byte[first.Length + second.Length];
            first.CopyTo(combined, 0);
            second.CopyTo(combined, first.Length);

            return combined;
        }
    }
}
