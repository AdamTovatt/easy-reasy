using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The relying party the WebAuthn tests are written against, and the assertion every parser test makes.
    /// </summary>
    internal static class WebAuthnTestData
    {
        /// <summary>The relying-party id the tests use.</summary>
        public const string RelyingPartyId = "example.com";

        /// <summary>An origin under <see cref="RelyingPartyId"/>.</summary>
        public const string Origin = "https://example.com";

        /// <summary>
        /// SHA-256 of <see cref="RelyingPartyId"/> — what an authenticator scoped to it reports. A property
        /// rather than a field so each caller gets its own array: a shared <c>static readonly byte[]</c> is
        /// writable by every test in the assembly.
        /// </summary>
        public static byte[] RelyingPartyIdHash => SHA256.HashData(Encoding.UTF8.GetBytes(RelyingPartyId));

        /// <summary>
        /// Asserts that parsing fails with the expected error, and returns the exception so a test can also
        /// pin the message when the message is what distinguishes two failures of the same kind.
        /// </summary>
        /// <param name="expectedError">The error the parser is expected to report.</param>
        /// <param name="parse">The parse call under test.</param>
        public static WebAuthnParseException AssertParseError(WebAuthnParseError expectedError, Action parse)
        {
            WebAuthnParseException exception = Assert.ThrowsException<WebAuthnParseException>(parse);
            Assert.AreEqual(expectedError, exception.Error);
            return exception;
        }
    }
}
