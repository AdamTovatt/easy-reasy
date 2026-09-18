namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Assertions about how the internal WebAuthn parsers fail.
    /// </summary>
    internal static class WebAuthnParseAssert
    {
        /// <summary>
        /// Asserts that parsing fails with the expected error, and returns the exception so a test can also
        /// pin the message when the message is what distinguishes two failures of the same kind.
        /// </summary>
        /// <param name="expectedError">The error the parser is expected to report.</param>
        /// <param name="parse">The parse call under test.</param>
        public static WebAuthnParseException Throws(WebAuthnParseError expectedError, Action parse)
        {
            WebAuthnParseException exception = Assert.ThrowsException<WebAuthnParseException>(parse);
            Assert.AreEqual(expectedError, exception.Error);
            return exception;
        }
    }
}
