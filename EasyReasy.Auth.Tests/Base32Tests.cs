using System.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class Base32Tests
    {
        // RFC 4648 §10 test vectors, without the padding the encoder omits.
        [DataTestMethod]
        [DataRow("", "")]
        [DataRow("f", "MY")]
        [DataRow("fo", "MZXQ")]
        [DataRow("foo", "MZXW6")]
        [DataRow("foob", "MZXW6YQ")]
        [DataRow("fooba", "MZXW6YTB")]
        [DataRow("foobar", "MZXW6YTBOI")]
        public void Encode_Rfc4648Vectors_ProducesExpectedString(string input, string expected)
        {
            string encoded = Base32.Encode(Encoding.ASCII.GetBytes(input));

            Assert.AreEqual(expected, encoded);
        }

        [DataTestMethod]
        [DataRow("MY", "f")]
        [DataRow("MZXQ", "fo")]
        [DataRow("MZXW6", "foo")]
        [DataRow("MZXW6YQ", "foob")]
        [DataRow("MZXW6YTB", "fooba")]
        [DataRow("MZXW6YTBOI", "foobar")]
        public void Decode_Rfc4648Vectors_ProducesExpectedBytes(string input, string expected)
        {
            byte[] decoded = Base32.Decode(input);

            Assert.AreEqual(expected, Encoding.ASCII.GetString(decoded));
        }

        [DataTestMethod]
        [DataRow("mzxw6ytboi")]
        [DataRow("MZXW6YTBOI========")]
        [DataRow("mzxw6ytboi======")]
        [DataRow("  MZXW6YTBOI  ")]
        [DataRow("\tMZXW6YTBOI\r\n")]
        [DataRow("  mzxw6ytboi======  ")]
        [DataRow("MZXW 6YTB OI")] // the space-grouped layout authenticator apps display
        [DataRow("mzxw 6ytb oi")]
        public void Decode_LenientInput_DecodesAsIfCanonical(string input)
        {
            byte[] decoded = Base32.Decode(input);

            Assert.AreEqual("foobar", Encoding.ASCII.GetString(decoded));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("========")]
        [DataRow("  ======  ")]
        public void Decode_NothingToDecode_ReturnsEmptyArray(string input)
        {
            byte[] decoded = Base32.Decode(input);

            Assert.AreEqual(0, decoded.Length);
        }

        [TestMethod]
        public void EncodeThenDecode_RoundTripsArbitraryBytes()
        {
            // Chosen for the boundaries a bit-packing loop gets wrong: a leading zero byte, an
            // all-ones byte, the sign bit alone, alternating bits, and a length (9) whose final group
            // needs padding.
            byte[] original = new byte[] { 0x00, 0x2A, 0xFF, 0x10, 0x99, 0x7C, 0x01, 0x80, 0x55 };

            byte[] roundTripped = Base32.Decode(Base32.Encode(original));

            CollectionAssert.AreEqual(original, roundTripped);
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(10)]
        [DataRow(20)]
        public void EncodeThenDecode_RoundTripsEveryTailLength(int byteCount)
        {
            byte[] original = new byte[byteCount];
            for (int index = 0; index < byteCount; index++)
            {
                original[index] = (byte)(index * 7 + 1);
            }

            byte[] roundTripped = Base32.Decode(Base32.Encode(original));

            CollectionAssert.AreEqual(original, roundTripped);
        }

        [DataTestMethod]
        [DataRow("MZXW6YTB0I")] // '0' is not in the alphabet — the digits start at '2'
        [DataRow("MZXW6YTB1I")] // nor is '1'
        [DataRow("MZXW6YTB-I")]
        // 12 characters, a count the encoder can produce, so this row fails on the '=' itself rather
        // than on the length check that fires first for an 11-character string.
        [DataRow("MZXW=6YTBOIA")] // padding is only tolerated at the end
        public void Decode_OutOfAlphabetCharacter_Throws(string input)
        {
            FormatException exception = Assert.ThrowsException<FormatException>(() => Base32.Decode(input));

            // Not just any FormatException: the count guard's message also mentions characters, so a
            // row that fell through to it would otherwise look like it had tested the alphabet.
            StringAssert.Contains(exception.Message, "Invalid base32 character");
        }

        [TestMethod]
        public void Decode_BadCharacterAndBadLength_ReportsTheCharacter()
        {
            // Three characters is a count no encoding produces, and '0' is outside the alphabet, so
            // both guards would fire. The character is the one a reader can see is wrong, so it has
            // to be the one reported.
            FormatException exception = Assert.ThrowsException<FormatException>(() => Base32.Decode("M0X"));

            StringAssert.Contains(exception.Message, "Invalid base32 character");
        }

        // 1, 3 and 6 characters over a multiple of eight is a group the encoder cannot produce, so the
        // input lost or gained a character. Decoding it would drop the leftover bits and return a
        // plausible wrong secret instead of reporting the typo.
        [DataTestMethod]
        [DataRow("M")]
        [DataRow("MZX")]
        [DataRow("MZXW6Y")]
        [DataRow("MZXW6YTBOIA")]
        [DataRow("MZXW 6YTB OIA")]
        [DataRow("MZXW6YTBOIA=====")]
        public void Decode_CharacterCountNoEncodingProduces_Throws(string input)
        {
            FormatException exception = Assert.ThrowsException<FormatException>(() => Base32.Decode(input));

            StringAssert.Contains(exception.Message, "cannot be the encoding of any byte sequence");
        }

        // The encoder zero-pads the final character, so bits set past the last whole byte mean a
        // character was mistyped into a neighbour — the slip a right-sized count cannot catch.
        // "MZXQ" is the canonical encoding of "fo"; the other three differ only in those pad bits.
        [DataTestMethod]
        [DataRow("MZXR")]
        [DataRow("MZXS")]
        [DataRow("MZXT")]
        [DataRow("MZXW6YTBOJ")]
        public void Decode_NonZeroPaddingBits_Throws(string input)
        {
            FormatException exception = Assert.ThrowsException<FormatException>(() => Base32.Decode(input));

            StringAssert.Contains(exception.Message, "bits past the final whole byte");
        }

        [TestMethod]
        public void Decode_CanonicalPaddingBits_IsAccepted()
        {
            byte[] decoded = Base32.Decode("MZXQ");

            Assert.AreEqual("fo", Encoding.ASCII.GetString(decoded));
        }

        [TestMethod]
        public void Decode_Null_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Base32.Decode(null!));
        }
    }
}
