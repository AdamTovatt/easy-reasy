namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// The few WebAuthn wire values a browser and an authenticator write, copied here rather than read
    /// from EasyReasy.Auth, which keeps its own copies internal.
    /// </summary>
    /// <remarks>
    /// These are values the WebAuthn Level 3 specification fixes, not library logic, so a copy cannot
    /// drift from the verifier's in any way the verifier would accept: a wrong one makes every ceremony
    /// this package writes fail verification.
    /// </remarks>
    internal static class WebAuthnWireValues
    {
        /// <summary>The <c>type</c> of the client data a registration collects (§5.8.1).</summary>
        public const string RegistrationCeremonyType = "webauthn.create";

        /// <summary>The <c>type</c> of the client data an authentication collects (§5.8.1).</summary>
        public const string AuthenticationCeremonyType = "webauthn.get";

        /// <summary>The one credential type WebAuthn defines (§5.8.2).</summary>
        public const string PublicKeyCredentialType = "public-key";

        /// <summary>The attestation statement format that attests nothing (§8.7).</summary>
        public const string NoneAttestationFormat = "none";
    }
}
