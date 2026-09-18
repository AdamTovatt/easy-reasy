using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class CoseKeyTests
    {
        private static readonly byte[] SignedData = Encoding.UTF8.GetBytes("authenticatorData||SHA-256(clientDataJSON)");

        [TestMethod]
        public void Parse_Es256Key_ReportsEs256AndVerifiesASignatureFromThatKey()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(CoseAlgorithm.Es256);

            using CoseKey key = CoseKey.Parse(authenticator.EncodeCoseKey());

            Assert.AreEqual(CoseAlgorithm.Es256, key.Algorithm);
            Assert.IsTrue(key.VerifySignature(SignedData, authenticator.Sign(SignedData)));
        }

        [TestMethod]
        public void Parse_Rs256Key_ReportsRs256AndVerifiesASignatureFromThatKey()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(CoseAlgorithm.Rs256);

            using CoseKey key = CoseKey.Parse(authenticator.EncodeCoseKey());

            Assert.AreEqual(CoseAlgorithm.Rs256, key.Algorithm);
            Assert.IsTrue(key.VerifySignature(SignedData, authenticator.Sign(SignedData)));
        }

        [DataTestMethod]
        [DataRow(CoseAlgorithm.Es256)]
        [DataRow(CoseAlgorithm.Rs256)]
        public void VerifySignature_SignatureOverDifferentBytes_ReturnsFalse(CoseAlgorithm algorithm)
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(algorithm);
            using CoseKey key = CoseKey.Parse(authenticator.EncodeCoseKey());

            byte[] signature = authenticator.Sign(Encoding.UTF8.GetBytes("some other bytes entirely"));

            Assert.IsFalse(key.VerifySignature(SignedData, signature));
        }

        [DataTestMethod]
        [DataRow(CoseAlgorithm.Es256)]
        [DataRow(CoseAlgorithm.Rs256)]
        public void VerifySignature_SignatureFromAnotherAuthenticator_ReturnsFalse(CoseAlgorithm algorithm)
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(algorithm);
            using SyntheticAuthenticator otherAuthenticator = SyntheticAuthenticator.Create(algorithm);
            using CoseKey key = CoseKey.Parse(authenticator.EncodeCoseKey());

            Assert.IsFalse(key.VerifySignature(SignedData, otherAuthenticator.Sign(SignedData)));
        }

        [DataTestMethod]
        [DataRow(CoseAlgorithm.Es256)]
        [DataRow(CoseAlgorithm.Rs256)]
        public void VerifySignature_SignatureThatIsNotEvenWellFormed_ReturnsFalseRatherThanThrowing(CoseAlgorithm algorithm)
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(algorithm);
            using CoseKey key = CoseKey.Parse(authenticator.EncodeCoseKey());

            Assert.IsFalse(key.VerifySignature(SignedData, new byte[] { 0xFF, 0xFF, 0xFF }));
        }

        [TestMethod]
        public void Parse_Es256KeyStatingTheRsaKeyType_Throws()
        {
            using Es256SyntheticAuthenticator authenticator = Es256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            ECParameters parameters = authenticator.ExportParameters();

            byte[] encoded = CborTestEncoder.EncodeCoseEc2Key(parameters.Q.X!, parameters.Q.Y!, keyType: 3);

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
        }

        [TestMethod]
        public void Parse_Es256KeyOnACurveOtherThanP256_Throws()
        {
            using Es256SyntheticAuthenticator authenticator = Es256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            ECParameters parameters = authenticator.ExportParameters();

            // Curve 2 is P-384; the coordinates are still the P-256 ones, so only the stated curve is wrong.
            byte[] encoded = CborTestEncoder.EncodeCoseEc2Key(parameters.Q.X!, parameters.Q.Y!, curve: 2);

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
        }

        [TestMethod]
        public void Parse_Es256KeyWithATruncatedCoordinate_Throws()
        {
            using Es256SyntheticAuthenticator authenticator = Es256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            ECParameters parameters = authenticator.ExportParameters();

            byte[] encoded = CborTestEncoder.EncodeCoseEc2Key(parameters.Q.X![..31], parameters.Q.Y!);

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
        }

        [DataTestMethod]
        [DataRow(-2)]
        [DataRow(-3)]
        public void Parse_Es256KeyMissingACoordinate_Throws(int omittedLabel)
        {
            using Es256SyntheticAuthenticator authenticator = Es256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            ECParameters parameters = authenticator.ExportParameters();

            CborMapBuilder builder = new CborMapBuilder()
                .With(CborTestEncoder.EncodeInteger(1), CborTestEncoder.EncodeInteger(2))
                .With(CborTestEncoder.EncodeInteger(3), CborTestEncoder.EncodeInteger(-7))
                .With(CborTestEncoder.EncodeInteger(-1), CborTestEncoder.EncodeInteger(1));

            if (omittedLabel != -2)
            {
                builder.With(CborTestEncoder.EncodeInteger(-2), CborTestEncoder.EncodeByteString(parameters.Q.X!));
            }

            if (omittedLabel != -3)
            {
                builder.With(CborTestEncoder.EncodeInteger(-3), CborTestEncoder.EncodeByteString(parameters.Q.Y!));
            }

            byte[] encoded = builder.Build();

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
        }

        [TestMethod]
        public void Parse_Es256KeyWhoseCoordinatesAreNotAPointOnTheCurve_Throws()
        {
            byte[] x = Enumerable.Repeat((byte)0xAA, 32).ToArray();
            byte[] y = Enumerable.Repeat((byte)0xBB, 32).ToArray();

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(CborTestEncoder.EncodeCoseEc2Key(x, y)));
        }

        [TestMethod]
        public void Parse_Rs256KeyStatingTheEc2KeyType_Throws()
        {
            using Rs256SyntheticAuthenticator authenticator = Rs256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            RSAParameters parameters = authenticator.ExportParameters();

            byte[] encoded = CborTestEncoder.EncodeCoseRsaKey(parameters.Modulus!, parameters.Exponent!, keyType: 2);

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
        }

        [TestMethod]
        public void Parse_RsaKeyMaterialStatingEs256_Throws()
        {
            // The stated algorithm selects how the rest of the map is read, so an RSA key claiming ES256 is
            // read as an EC2 key and rejected on the key type rather than reinterpreted.
            using Rs256SyntheticAuthenticator authenticator = Rs256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            RSAParameters parameters = authenticator.ExportParameters();

            byte[] encoded = CborTestEncoder.EncodeCoseRsaKey(parameters.Modulus!, parameters.Exponent!, algorithm: -7);

            WebAuthnParseException exception = WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
            StringAssert.Contains(exception.Message, "states ES256");
        }

        [TestMethod]
        public void Parse_Rs256KeyWithA1024BitModulus_Throws()
        {
            using RSA weakKey = RSA.Create(1024);
            RSAParameters parameters = weakKey.ExportParameters(false);

            byte[] encoded = CborTestEncoder.EncodeCoseRsaKey(parameters.Modulus!, parameters.Exponent!);

            WebAuthnParseException exception = WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
            StringAssert.Contains(exception.Message, "1024-bit modulus");
        }

        [TestMethod]
        public void Parse_Rs256KeyWithAnAbsurdlyLargeModulus_Throws()
        {
            // The modulus arrives from an untrusted response and every later verification is modular
            // arithmetic over it, so the size an attacker can choose is bounded.
            byte[] hugeModulus = Enumerable.Repeat((byte)0xFF, 2048).ToArray();

            byte[] encoded = CborTestEncoder.EncodeCoseRsaKey(hugeModulus, new byte[] { 0x01, 0x00, 0x01 });

            WebAuthnParseException exception = WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
            StringAssert.Contains(exception.Message, "16384-bit modulus");
        }

        [TestMethod]
        public void Parse_Rs256KeyWhoseModulusCarriesALeadingZero_StillVerifiesSignaturesFromThatKey()
        {
            using Rs256SyntheticAuthenticator authenticator = Rs256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            RSAParameters parameters = authenticator.ExportParameters();

            byte[] paddedModulus = new byte[parameters.Modulus!.Length + 1];
            parameters.Modulus.CopyTo(paddedModulus, 1);

            using CoseKey key = CoseKey.Parse(CborTestEncoder.EncodeCoseRsaKey(paddedModulus, parameters.Exponent!));

            Assert.IsTrue(key.VerifySignature(SignedData, authenticator.Sign(SignedData)));
        }

        [TestMethod]
        public void Parse_Rs256KeyWithAnEmptyExponent_Throws()
        {
            using Rs256SyntheticAuthenticator authenticator = Rs256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            RSAParameters parameters = authenticator.ExportParameters();

            byte[] encoded = CborTestEncoder.EncodeCoseRsaKey(parameters.Modulus!, Array.Empty<byte>());

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
        }

        [DataTestMethod]
        [DataRow(-8L)]   // EdDSA
        [DataRow(-35L)]  // ES384
        [DataRow(-37L)]  // PS256
        [DataRow(-65535L)] // RS1
        public void Parse_KeyStatingAnAlgorithmOutsideTheSupportedSet_ThrowsUnsupportedAlgorithm(long algorithm)
        {
            using Es256SyntheticAuthenticator authenticator = Es256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            ECParameters parameters = authenticator.ExportParameters();

            byte[] encoded = CborTestEncoder.EncodeCoseEc2Key(parameters.Q.X!, parameters.Q.Y!, algorithm: algorithm);

            WebAuthnParseAssert.Throws(WebAuthnParseError.UnsupportedAlgorithm, () => CoseKey.Parse(encoded));
        }

        [TestMethod]
        public void Parse_KeyWithNoKeyType_Throws()
        {
            byte[] encoded = new CborMapBuilder()
                .With(CborTestEncoder.EncodeInteger(3), CborTestEncoder.EncodeInteger(-7))
                .Build();

            WebAuthnParseException exception = WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
            StringAssert.Contains(exception.Message, "no key type");
        }

        [TestMethod]
        public void Parse_KeyWithNoAlgorithm_Throws()
        {
            byte[] encoded = new CborMapBuilder()
                .With(CborTestEncoder.EncodeInteger(1), CborTestEncoder.EncodeInteger(2))
                .Build();

            WebAuthnParseException exception = WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
            StringAssert.Contains(exception.Message, "no algorithm");
        }

        [TestMethod]
        public void Parse_KeyCarryingTheSameLabelTwiceUnderDifferentEncodings_Throws()
        {
            // CBOR Strict mode compares encoded key bytes, so the same label written minimally and
            // non-minimally is not a repeat to the reader. Without the parser's own guard the second value
            // would simply win, and a key could carry two algorithms.
            using Es256SyntheticAuthenticator authenticator = Es256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            ECParameters parameters = authenticator.ExportParameters();

            byte[] encoded = new CborMapBuilder()
                .With(CborTestEncoder.EncodeInteger(1), CborTestEncoder.EncodeInteger(2))
                .With(CborTestEncoder.EncodeInteger(3), CborTestEncoder.EncodeInteger(-7))
                .With(CborTestEncoder.EncodeIntegerNonMinimally(3), CborTestEncoder.EncodeInteger(-257))
                .With(CborTestEncoder.EncodeInteger(-1), CborTestEncoder.EncodeInteger(1))
                .With(CborTestEncoder.EncodeInteger(-2), CborTestEncoder.EncodeByteString(parameters.Q.X!))
                .With(CborTestEncoder.EncodeInteger(-3), CborTestEncoder.EncodeByteString(parameters.Q.Y!))
                .Build();

            WebAuthnParseException exception = WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(encoded));
            StringAssert.Contains(exception.Message, "repeats label 3");
        }

        [TestMethod]
        public void Parse_KeyCarryingLabelsThisLibraryDoesNotRead_IgnoresThem()
        {
            // COSE is extensible; a key carrying a text-string label, or a label whose value is neither an
            // integer nor a byte string, still parses on the labels that matter.
            using Es256SyntheticAuthenticator authenticator = Es256SyntheticAuthenticator.Create(new byte[] { 0x01 });
            ECParameters parameters = authenticator.ExportParameters();

            byte[] encoded = new CborMapBuilder()
                .With(CborTestEncoder.EncodeTextString("comment"), CborTestEncoder.EncodeTextString("ignored"))
                .With(CborTestEncoder.EncodeInteger(1), CborTestEncoder.EncodeInteger(2))
                .With(CborTestEncoder.EncodeInteger(3), CborTestEncoder.EncodeInteger(-7))
                .With(CborTestEncoder.EncodeInteger(4), CborTestEncoder.EncodeEmptyMap())
                .With(CborTestEncoder.EncodeInteger(-1), CborTestEncoder.EncodeInteger(1))
                .With(CborTestEncoder.EncodeInteger(-2), CborTestEncoder.EncodeByteString(parameters.Q.X!))
                .With(CborTestEncoder.EncodeInteger(-3), CborTestEncoder.EncodeByteString(parameters.Q.Y!))
                .Build();

            using CoseKey key = CoseKey.Parse(encoded);

            Assert.AreEqual(CoseAlgorithm.Es256, key.Algorithm);
            Assert.IsTrue(key.VerifySignature(SignedData, authenticator.Sign(SignedData)));
        }

        [TestMethod]
        public void Parse_KeyFollowedByTrailingBytes_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            byte[] coseKey = authenticator.EncodeCoseKey();

            byte[] withTrailingBytes = new byte[coseKey.Length + 2];
            coseKey.CopyTo(withTrailingBytes, 0);

            WebAuthnParseException exception = WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(withTrailingBytes));
            StringAssert.Contains(exception.Message, "not part of it");
        }

        [TestMethod]
        public void Parse_BytesThatAreNotCbor_Throws()
        {
            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));
        }

        [TestMethod]
        public void Parse_CborThatIsNotAMap_Throws()
        {
            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedPublicKey, () => CoseKey.Parse(new byte[] { 0x01 }));
        }
    }
}
