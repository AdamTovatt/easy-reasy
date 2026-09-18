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
