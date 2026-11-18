using System.Formats.Cbor;
using System.Security.Cryptography;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Reads the raw public parameters back out of a COSE public key, for a test that re-encodes a test
    /// authenticator's key with one label deliberately wrong, or checks a signature outside the library.
    /// </summary>
    /// <remarks>
    /// The test authenticator keeps its raw key parameters private and publishes only the COSE key, which
    /// is all a real authenticator publishes too; this is the helper its README says such a test writes.
    /// Deliberately separate from the library's own COSE parser, so a test checking that parser does not
    /// lean on it to build its inputs.
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
