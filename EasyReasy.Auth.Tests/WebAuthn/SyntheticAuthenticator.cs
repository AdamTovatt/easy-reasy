namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A WebAuthn authenticator implemented in the test assembly: it holds a real key pair, publishes the
    /// COSE encoding of its public key, and signs the bytes a real authenticator would sign. That is what
    /// makes every verification check provable — a test assembles a ceremony that is correct, then mutates
    /// exactly the field the check reads and shows verification fails.
    /// </summary>
    /// <remarks>
    /// Split by algorithm the same way <c>CoseKey</c> is, so neither side carries a key it does not use.
    /// </remarks>
    internal abstract class SyntheticAuthenticator : IDisposable
    {
        /// <summary>
        /// The model identifier a synthetic authenticator reports. A random version-4 GUID on purpose: an
        /// AAGUID assigned to a real vendor would read as a claim about that vendor's hardware.
        /// </summary>
        public static readonly Guid SyntheticAaguid = new Guid("6d1f2a4e-3c7b-4a90-9e5d-8f2c1b0a7d64");

        private static readonly byte[] DefaultCredentialId = new byte[] { 0xC0, 0xDE, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

        /// <summary>The algorithm this authenticator signs with.</summary>
        public abstract CoseAlgorithm Algorithm { get; }

        /// <summary>The credential id this authenticator reports.</summary>
        public byte[] CredentialId { get; }

        /// <summary>The model identifier this authenticator reports.</summary>
        public Guid Aaguid => SyntheticAaguid;

        protected SyntheticAuthenticator(byte[] credentialId)
        {
            CredentialId = credentialId;
        }

        /// <summary>
        /// Creates an authenticator with a freshly generated key pair.
        /// </summary>
        /// <param name="algorithm">The algorithm to sign with.</param>
        /// <param name="credentialId">The credential id to report; a distinctive default is used when omitted.</param>
        public static SyntheticAuthenticator Create(CoseAlgorithm algorithm = CoseAlgorithm.Es256, byte[]? credentialId = null)
        {
            byte[] effectiveCredentialId = credentialId ?? DefaultCredentialId;

            return algorithm == CoseAlgorithm.Es256
                ? Es256SyntheticAuthenticator.Create(effectiveCredentialId)
                : Rs256SyntheticAuthenticator.Create(effectiveCredentialId);
        }

        /// <summary>
        /// The CBOR-encoded COSE public key this authenticator would hand back at registration.
        /// </summary>
        public abstract byte[] EncodeCoseKey();

        /// <summary>
        /// Signs the given bytes the way the algorithm requires on the WebAuthn wire — an ASN.1 DER
        /// sequence for ECDSA, PKCS#1 v1.5 for RSA.
        /// </summary>
        public abstract byte[] Sign(ReadOnlySpan<byte> data);

        /// <inheritdoc />
        public abstract void Dispose();
    }
}
