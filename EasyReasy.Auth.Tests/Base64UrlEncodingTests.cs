using System.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class Base64UrlEncodingTests
    {
        [DataTestMethod]
        [DataRow(new byte[] { 0xFB, 0xFF, 0xBF })] // every character differs between the two alphabets
        [DataRow(new byte[] { 0x01 })]             // two padding characters in standard base64
        [DataRow(new byte[] { 0x01, 0x02 })]       // one padding character
        [DataRow(new byte[] { 0x01, 0x02, 0x03 })] // none
        [DataRow(new byte[0])]
        public void Encode_MatchesTheUrlSafeTransformOfStandardBase64(byte[] value)
        {
            // The expression on the right is what RefreshTokenService and SecurePasswordResetTokenHandler
            // each spelled out before they moved onto this type. Written out here rather than referenced,
            // it is an independent oracle that the move changed no token's shape.
            string expected = Convert.ToBase64String(value)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            Assert.AreEqual(expected, Base64UrlEncoding.Encode(value));
        }

        [TestMethod]
        public void Encode_UsesTheUrlSafeAlphabet()
        {
            Assert.AreEqual("-_-_", Base64UrlEncoding.Encode(new byte[] { 0xFB, 0xFF, 0xBF }));
        }

        [TestMethod]
        public void TryDecode_WhatEncodeProduced_ReturnsTheOriginalBytes()
        {
            byte[] value = new byte[] { 0xFB, 0xFF, 0xBF, 0x01 };

            CollectionAssert.AreEqual(value, Base64UrlEncoding.TryDecode(Base64UrlEncoding.Encode(value)));
        }

        [DataTestMethod]
        [DataRow("Y3JlZGVudGlhbC1vbmU")]    // canonical
        [DataRow("Y3JlZGVudGlhbC1vbmU=")]   // padded
        [DataRow("Y3JlZGVudGlh bC1vbmU")]   // embedded whitespace
        public void TryDecode_AnySpellingOfTheSameBytes_DecodesToThem(string value)
        {
            // Laxer than IsCanonical on purpose: these arrive from a browser, and a client library that
            // pads or wraps its base64url has still named the right bytes.
            Assert.AreEqual("credential-one", Encoding.UTF8.GetString(Base64UrlEncoding.TryDecode(value)!));
        }

        [DataTestMethod]
        [DataRow("not base64url!")]
        [DataRow("+/+/")]                   // the standard alphabet, which base64url does not accept
        [DataRow("Y3JlZGVudGlhbC1vbmU==")]  // more padding than any length calls for
        public void TryDecode_TextThatIsNotBase64Url_ReturnsNull(string value)
        {
            // Null rather than empty: an empty result would be indistinguishable from text that legitimately
            // decodes to nothing, and the two are reported differently to whoever reads the log.
            Assert.IsNull(Base64UrlEncoding.TryDecode(value));
        }

        [TestMethod]
        public void IsCanonical_WhatEncodeProduces_IsCanonical()
        {
            string encoded = Base64UrlEncoding.Encode(Encoding.UTF8.GetBytes("credential-one"));

            Assert.IsTrue(Base64UrlEncoding.IsCanonical(encoded));
        }

        [DataTestMethod]
        [DataRow("Y3JlZGVudGlhbC1vbmU=")]   // padded
        [DataRow("Y3JlZGVudGlh bC1vbmU")]   // embedded whitespace
        [DataRow("Y3JlZGVudGlhbC1vbmU\n")]  // trailing newline
        [DataRow("+/+/")]                   // the standard alphabet
        [DataRow("not base64url!")]
        public void IsCanonical_AnythingElse_IsNot(string value)
        {
            // All five decode or half-decode somewhere, which is why merely checking validity let them
            // through to the browser as written.
            Assert.IsFalse(Base64UrlEncoding.IsCanonical(value));
        }
    }
}
