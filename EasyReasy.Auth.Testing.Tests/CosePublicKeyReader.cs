using System.Formats.Cbor;
using System.Security.Cryptography;

namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// Reads the raw public parameters back out of the authenticator's COSE public key, to check its
    /// signatures and its <c>publicKey</c> outside the library.
    /// </summary>
    /// <remarks>
    /// This project's own copy of the helper the package README tells a consumer to write, rather than a
    /// file linked out of EasyReasy.Auth.Tests: the two test projects stay independent, as a consumer's is.
    /// </remarks>
    internal static class CosePublicKeyReader
    {
        private const int Ec2XLabel = -2;
        private const int Ec2YLabel = -3;
        private const int RsaModulusLabel = -1;
        private const int RsaExponentLabel = -2;

        /// <summary>Reads a COSE EC2 key's P-256 coordinates.</summary>
        /// <param name="coseKey">The CBOR-encoded COSE EC2 key.</param>
        public static ECParameters ReadEs256(byte[] coseKey)
        {
            Dictionary<long, byte[]> byteStrings = ReadByteStringLabels(coseKey);

            return new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = byteStrings[Ec2XLabel],
                    Y = byteStrings[Ec2YLabel],
                },
            };
        }

        /// <summary>Reads a COSE RSA key's modulus and exponent.</summary>
        /// <param name="coseKey">The CBOR-encoded COSE RSA key.</param>
        public static RSAParameters ReadRs256(byte[] coseKey)
        {
            Dictionary<long, byte[]> byteStrings = ReadByteStringLabels(coseKey);

            return new RSAParameters
            {
                Modulus = byteStrings[RsaModulusLabel],
                Exponent = byteStrings[RsaExponentLabel],
            };
        }

        /// <summary>Reads every label of the map whose value is a byte string.</summary>
        private static Dictionary<long, byte[]> ReadByteStringLabels(byte[] coseKey)
        {
            CborReader reader = new CborReader(coseKey, CborConformanceMode.Lax);
            Dictionary<long, byte[]> byteStrings = new Dictionary<long, byte[]>();

            int? entryCount = reader.ReadStartMap();
            for (int index = 0; index < entryCount; index++)
            {
                long label = reader.ReadInt64();

                if (reader.PeekState() == CborReaderState.ByteString)
                {
                    byteStrings[label] = reader.ReadByteString();
                }
                else
                {
                    reader.SkipValue();
                }
            }

            reader.ReadEndMap();

            return byteStrings;
        }
    }
}
