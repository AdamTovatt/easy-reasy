using System.Security.Cryptography;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A <see cref="SyntheticAuthenticator"/> holding a real P-256 key pair and signing ES256.
    /// </summary>
    internal sealed class Es256SyntheticAuthenticator : SyntheticAuthenticator
    {
        private readonly ECDsa _key;

        private Es256SyntheticAuthenticator(ECDsa key, byte[] credentialId)
            : base(credentialId)
        {
            _key = key;
        }

        /// <inheritdoc />
        public override CoseAlgorithm Algorithm => CoseAlgorithm.Es256;

        /// <summary>Creates an authenticator with a freshly generated P-256 key pair.</summary>
        public static Es256SyntheticAuthenticator Create(byte[] credentialId)
        {
            return new Es256SyntheticAuthenticator(ECDsa.Create(ECCurve.NamedCurves.nistP256), credentialId);
        }

        /// <summary>
        /// This authenticator's public key material, so a test can re-encode it with one COSE label
        /// deliberately wrong.
        /// </summary>
        public ECParameters ExportParameters()
        {
            return _key.ExportParameters(false);
        }

        /// <inheritdoc />
        public override byte[] EncodeCoseKey()
        {
            ECParameters parameters = ExportParameters();
            return CborTestEncoder.EncodeCoseEc2Key(parameters.Q.X!, parameters.Q.Y!);
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
