using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A COSE_Key holding an ECDSA P-256 public key, the ES256 half of <see cref="CoseKey"/>.
    /// </summary>
    internal sealed class Es256CoseKey : CoseKey
    {
        /// <summary>COSE key type for a two-coordinate elliptic-curve key (RFC 8152 §13).</summary>
        private const int Ec2KeyType = 2;

        /// <summary>COSE curve identifier for NIST P-256 (RFC 8152 §13.1).</summary>
        private const int P256Curve = 1;

        /// <summary>Length of each P-256 coordinate, which is fixed and zero-padded on the left.</summary>
        private const int P256CoordinateLength = 32;

        private readonly ECDsa _key;

        private Es256CoseKey(ECDsa key)
        {
            _key = key;
        }

        /// <inheritdoc />
        public override CoseAlgorithm Algorithm => CoseAlgorithm.Es256;

        /// <summary>
        /// Builds the key from the labels a COSE_Key map carried, having already established that it states
        /// ES256.
        /// </summary>
        /// <exception cref="WebAuthnParseException">The labels do not describe a P-256 public key.</exception>
        public static Es256CoseKey Create(long keyType, Dictionary<int, long> integerValues, Dictionary<int, byte[]> byteStringValues)
        {
            if (keyType != Ec2KeyType)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE key states ES256 but key type {keyType} rather than EC2 ({Ec2KeyType}).");
            }

            if (!integerValues.TryGetValue(FirstKeyTypeSpecificLabel, out long curve) || curve != P256Curve)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, "COSE ES256 key is not on curve P-256.");
            }

            if (!byteStringValues.TryGetValue(SecondKeyTypeSpecificLabel, out byte[]? x) || x.Length != P256CoordinateLength
                || !byteStringValues.TryGetValue(ThirdKeyTypeSpecificLabel, out byte[]? y) || y.Length != P256CoordinateLength)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE ES256 key must carry {P256CoordinateLength}-byte x and y coordinates.");
            }

            try
            {
                ECParameters parameters = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = x, Y = y },
                };

                return new Es256CoseKey(ECDsa.Create(parameters));
            }
            catch (CryptographicException exception)
            {
                // Importing validates the point, so a coordinate pair that is not on P-256 lands here.
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE ES256 key is not a valid P-256 public key: {exception.Message}");
            }
        }

        /// <inheritdoc />
        public override bool VerifySignature(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
        {
            try
            {
                // WebAuthn carries an ECDSA signature as an ASN.1 DER SEQUENCE of r and s, not as the
                // fixed-width concatenation .NET defaults to.
                return _key.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            }
            catch (CryptographicException)
            {
                // A signature that is not even decodable is a failed verification, not an error.
                return false;
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _key.Dispose();
        }
    }
}
