using System.Formats.Cbor;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// Writes the CBOR structures an honest authenticator produces: its COSE public key and the
    /// attestation object of a <c>none</c> attestation.
    /// </summary>
    /// <remarks>
    /// Written in <see cref="CborConformanceMode.Ctap2Canonical"/>, the encoding CTAP2 requires of a real
    /// authenticator, so the writer itself refuses a duplicate key and orders every map the way a security
    /// key does. Nothing here can write a malformed structure; a test that needs one builds it from the
    /// public <see cref="WebAuthnTestAuthenticator.CosePublicKey"/> and
    /// <see cref="WebAuthnTestAuthenticator.Sign"/> in its own helpers.
    /// </remarks>
    internal static class WebAuthnTestCbor
    {
        private const int KeyTypeLabel = 1;
        private const int AlgorithmLabel = 3;

        private const int Ec2KeyType = 2;
        private const int RsaKeyType = 3;

        private const int Ec2CurveLabel = -1;
        private const int Ec2XLabel = -2;
        private const int Ec2YLabel = -3;
        private const int P256Curve = 1;

        private const int RsaModulusLabel = -1;
        private const int RsaExponentLabel = -2;

        /// <summary>
        /// Encodes a P-256 public key as a COSE EC2 key (RFC 9053 §7.1.1) stating ES256.
        /// </summary>
        /// <param name="x">The x coordinate, 32 bytes.</param>
        /// <param name="y">The y coordinate, 32 bytes.</param>
        public static byte[] EncodeEs256PublicKey(byte[] x, byte[] y)
        {
            CborWriter writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

            writer.WriteStartMap(5);
            writer.WriteInt32(KeyTypeLabel);
            writer.WriteInt32(Ec2KeyType);
            writer.WriteInt32(AlgorithmLabel);
            writer.WriteInt32((int)CoseAlgorithm.Es256);
            writer.WriteInt32(Ec2CurveLabel);
            writer.WriteInt32(P256Curve);
            writer.WriteInt32(Ec2XLabel);
            writer.WriteByteString(x);
            writer.WriteInt32(Ec2YLabel);
            writer.WriteByteString(y);
            writer.WriteEndMap();

            return writer.Encode();
        }

        /// <summary>
        /// Encodes an RSA public key as a COSE RSA key (RFC 8230 §4) stating RS256.
        /// </summary>
        /// <param name="modulus">The modulus, big-endian.</param>
        /// <param name="exponent">The public exponent, big-endian.</param>
        public static byte[] EncodeRs256PublicKey(byte[] modulus, byte[] exponent)
        {
            CborWriter writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

            writer.WriteStartMap(4);
            writer.WriteInt32(KeyTypeLabel);
            writer.WriteInt32(RsaKeyType);
            writer.WriteInt32(AlgorithmLabel);
            writer.WriteInt32((int)CoseAlgorithm.Rs256);
            writer.WriteInt32(RsaModulusLabel);
            writer.WriteByteString(modulus);
            writer.WriteInt32(RsaExponentLabel);
            writer.WriteByteString(exponent);
            writer.WriteEndMap();

            return writer.Encode();
        }

        /// <summary>
        /// Encodes the attestation object of a <c>none</c> attestation (WebAuthn Level 3 §6.5.4, §8.7):
        /// the format, an empty attestation statement, and the authenticator data.
        /// </summary>
        /// <param name="authenticatorData">The authenticator data, carrying the attested credential data.</param>
        public static byte[] EncodeNoneAttestationObject(byte[] authenticatorData)
        {
            CborWriter writer = new CborWriter(CborConformanceMode.Ctap2Canonical);

            writer.WriteStartMap(3);
            writer.WriteTextString("fmt");
            writer.WriteTextString(WebAuthnWireValues.NoneAttestationFormat);
            writer.WriteTextString("attStmt");
            writer.WriteStartMap(0);
            writer.WriteEndMap();
            writer.WriteTextString("authData");
            writer.WriteByteString(authenticatorData);
            writer.WriteEndMap();

            return writer.Encode();
        }
    }
}
