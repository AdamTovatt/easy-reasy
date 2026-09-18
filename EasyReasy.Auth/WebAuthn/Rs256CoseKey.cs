using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A COSE_Key holding an RSA public key, the RS256 half of <see cref="CoseKey"/>.
    /// </summary>
    internal sealed class Rs256CoseKey : CoseKey
    {
        /// <summary>COSE key type for an RSA key (RFC 8230 §4).</summary>
        private const int RsaKeyType = 3;

        /// <summary>Smallest accepted modulus: 2048 bits, the floor for RS256 in current practice.</summary>
        private const int MinimumModulusLength = 256;

        /// <summary>
        /// Largest accepted modulus: 8192 bits. The bound exists because the modulus arrives from an
        /// untrusted response and every later signature verification is modular arithmetic over it, so an
        /// absurdly large one would be work an attacker chose for us. No authenticator issues a key
        /// anywhere near this size.
        /// </summary>
        private const int MaximumModulusLength = 1024;

        private readonly RSA _key;

        private Rs256CoseKey(RSA key)
        {
            _key = key;
        }

        /// <inheritdoc />
        public override CoseAlgorithm Algorithm => CoseAlgorithm.Rs256;

        /// <summary>
        /// Builds the key from the labels a COSE_Key map carried, having already established that it states
        /// RS256.
        /// </summary>
        /// <exception cref="WebAuthnParseException">The labels do not describe an RSA public key of an accepted size.</exception>
        public static Rs256CoseKey Create(long keyType, Dictionary<int, byte[]> byteStringValues)
        {
            if (keyType != RsaKeyType)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE key states RS256 but key type {keyType} rather than RSA ({RsaKeyType}).");
            }

            if (!byteStringValues.TryGetValue(FirstKeyTypeSpecificLabel, out byte[]? modulus)
                || !byteStringValues.TryGetValue(SecondKeyTypeSpecificLabel, out byte[]? exponent))
            {
                throw new WebAuthnParseException(
                    WebAuthnParseError.MalformedPublicKey,
                    $"COSE RS256 key must carry a modulus (label {FirstKeyTypeSpecificLabel}) and an exponent (label {SecondKeyTypeSpecificLabel}).");
            }

            byte[] trimmedModulus = TrimLeadingZeroes(modulus);
            byte[] trimmedExponent = TrimLeadingZeroes(exponent);

            if (trimmedModulus.Length < MinimumModulusLength || trimmedModulus.Length > MaximumModulusLength)
            {
                throw new WebAuthnParseException(
                    WebAuthnParseError.MalformedPublicKey,
                    $"COSE RS256 key has a {trimmedModulus.Length * 8}-bit modulus; only {MinimumModulusLength * 8} to {MaximumModulusLength * 8} bits are accepted.");
            }

            if (trimmedExponent.Length == 0)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, "COSE RS256 key has an empty exponent.");
            }

            try
            {
                RSAParameters parameters = new RSAParameters
                {
                    Modulus = trimmedModulus,
                    Exponent = trimmedExponent,
                };

                return new Rs256CoseKey(RSA.Create(parameters));
            }
            catch (CryptographicException exception)
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE RS256 key is not a valid RSA public key: {exception.Message}");
            }
        }

        /// <inheritdoc />
        public override bool VerifySignature(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
        {
            try
            {
                return _key.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
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

        /// <summary>
        /// Drops leading zero bytes, which carry no value in the unsigned big-endian integers COSE uses for
        /// RSA parameters but do change the length <see cref="RSAParameters"/> reads as the key size.
        /// </summary>
        private static byte[] TrimLeadingZeroes(byte[] value)
        {
            int firstNonZero = 0;
            while (firstNonZero < value.Length && value[firstNonZero] == 0)
            {
                firstNonZero++;
            }

            return firstNonZero == 0 ? value : value[firstNonZero..];
        }
    }
}
