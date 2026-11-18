using System.Security.Cryptography;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// A <see cref="WebAuthnTestCredentialKey"/> holding a P-256 key pair and signing ES256.
    /// </summary>
    internal sealed class Es256WebAuthnTestCredentialKey : WebAuthnTestCredentialKey
    {
        private readonly ECDsa _key;

        private Es256WebAuthnTestCredentialKey(ECDsa key)
        {
            _key = key;
            SubjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo();

            ECParameters parameters = key.ExportParameters(false);
            CosePublicKey = WebAuthnTestCbor.EncodeEs256PublicKey(parameters.Q.X!, parameters.Q.Y!);
        }

        /// <inheritdoc />
        public override CoseAlgorithm Algorithm => CoseAlgorithm.Es256;

        /// <inheritdoc />
        public override byte[] CosePublicKey { get; }

        /// <inheritdoc />
        public override byte[] SubjectPublicKeyInfo { get; }

        /// <summary>Generates a fresh P-256 key pair.</summary>
        public static Es256WebAuthnTestCredentialKey Create()
        {
            return new Es256WebAuthnTestCredentialKey(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        }

        /// <inheritdoc />
        public override byte[] Sign(ReadOnlySpan<byte> data)
        {
            return _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _key.Dispose();
        }
    }
}
