using System.Security.Cryptography;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// A <see cref="WebAuthnTestCredentialKey"/> holding a 2048-bit RSA key pair and signing RS256.
    /// </summary>
    internal sealed class Rs256WebAuthnTestCredentialKey : WebAuthnTestCredentialKey
    {
        private readonly RSA _key;

        private Rs256WebAuthnTestCredentialKey(RSA key)
        {
            _key = key;
            SubjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo();

            RSAParameters parameters = key.ExportParameters(false);
            CosePublicKey = WebAuthnTestCbor.EncodeRs256PublicKey(parameters.Modulus!, parameters.Exponent!);
        }

        /// <inheritdoc />
        public override CoseAlgorithm Algorithm => CoseAlgorithm.Rs256;

        /// <inheritdoc />
        public override byte[] CosePublicKey { get; }

        /// <inheritdoc />
        public override byte[] SubjectPublicKeyInfo { get; }

        /// <summary>Generates a fresh 2048-bit RSA key pair.</summary>
        public static Rs256WebAuthnTestCredentialKey Create()
        {
            return new Rs256WebAuthnTestCredentialKey(RSA.Create(2048));
        }

        /// <inheritdoc />
        public override byte[] Sign(ReadOnlySpan<byte> data)
        {
            return _key.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _key.Dispose();
        }
    }
}
