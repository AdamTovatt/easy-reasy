namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// The key pair behind a <see cref="WebAuthnTestAuthenticator"/>: it publishes the COSE encoding of its
    /// public key and signs the bytes a real authenticator signs.
    /// </summary>
    /// <remarks>
    /// Split by algorithm the way EasyReasy.Auth's own COSE key is, so neither side carries a key it does
    /// not use. The private key never leaves this type: a test that needs the raw public parameters reads
    /// them out of <see cref="CosePublicKey"/>.
    /// </remarks>
    internal abstract class WebAuthnTestCredentialKey : IDisposable
    {
        /// <summary>The algorithm this key signs with.</summary>
        public abstract CoseAlgorithm Algorithm { get; }

        /// <summary>The CBOR-encoded COSE public key, encoded once when the key was made.</summary>
        public abstract byte[] CosePublicKey { get; }

        /// <summary>
        /// The DER SubjectPublicKeyInfo of the public key, which the browser's <c>toJSON()</c> reports as a
        /// registration's <c>publicKey</c>.
        /// </summary>
        public abstract byte[] SubjectPublicKeyInfo { get; }

        /// <summary>
        /// Generates a fresh key pair for the given algorithm.
        /// </summary>
        /// <param name="algorithm">The algorithm to sign with.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="algorithm"/> is not one this package signs with.</exception>
        public static WebAuthnTestCredentialKey Create(CoseAlgorithm algorithm)
        {
            return algorithm switch
            {
                CoseAlgorithm.Es256 => Es256WebAuthnTestCredentialKey.Create(),
                CoseAlgorithm.Rs256 => Rs256WebAuthnTestCredentialKey.Create(),
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, $"A test authenticator signs with {CoseAlgorithm.Es256} or {CoseAlgorithm.Rs256}."),
            };
        }

        /// <summary>
        /// Signs the given bytes the way the algorithm requires on the WebAuthn wire: an ASN.1 DER sequence
        /// for ECDSA, PKCS#1 v1.5 for RSA.
        /// </summary>
        /// <param name="data">The bytes to sign.</param>
        public abstract byte[] Sign(ReadOnlySpan<byte> data);

        /// <inheritdoc />
        public abstract void Dispose();
    }
}
