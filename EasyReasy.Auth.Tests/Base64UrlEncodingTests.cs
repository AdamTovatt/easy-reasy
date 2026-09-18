using System.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class Base64UrlEncodingTests
    {
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
