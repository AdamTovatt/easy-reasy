using System.Security.Cryptography;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A <see cref="SyntheticAuthenticator"/> holding a real 2048-bit RSA key pair and signing RS256.
    /// </summary>
    internal sealed class Rs256SyntheticAuthenticator : SyntheticAuthenticator
    {
        private readonly RSA _key;

        private Rs256SyntheticAuthenticator(RSA key, byte[] credentialId)
            : base(credentialId)
        {
            _key = key;
        }

        /// <inheritdoc />
        public override CoseAlgorithm Algorithm => CoseAlgorithm.Rs256;

        /// <summary>Creates an authenticator with a freshly generated 2048-bit RSA key pair.</summary>
        public static Rs256SyntheticAuthenticator Create(byte[] credentialId)
        {
            return new Rs256SyntheticAuthenticator(RSA.Create(2048), credentialId);
        }

        /// <summary>
        /// This authenticator's public key material, so a test can re-encode it with one COSE label
        /// deliberately wrong.
        /// </summary>
        public RSAParameters ExportParameters()
        {
            return _key.ExportParameters(false);
        }

        /// <inheritdoc />
        public override byte[] EncodeCoseKey()
        {
            RSAParameters parameters = ExportParameters();
            return CborTestEncoder.EncodeCoseRsaKey(parameters.Modulus!, parameters.Exponent!);
        }

        /// <inheritdoc />
        public override byte[] Sign(ReadOnlySpan<byte> data)
        {
            return _key.SignData(data.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _key.Dispose();
        }
    }
}
