namespace EasyReasy.Auth
{
    /// <summary>
    /// Thrown by the internal WebAuthn parsers when a structure cannot be read. Never escapes the library:
    /// each verifier catches it at its boundary and turns it into a failure reason on the result it returns,
    /// so a malformed ceremony is an outcome to the caller and not an exception.
    /// </summary>
    /// <remarks>
    /// Parsing a WebAuthn response walks several nested formats — JSON, base64url, CBOR, COSE — and every
    /// step of every one of them can fail. Carrying that as an exception keeps the parsers linear instead of
    /// threading a result through each read, and the single catch in the verifier is what stops it becoming
    /// part of the public contract.
    /// </remarks>
    internal sealed class WebAuthnParseException : Exception
    {
        /// <summary>What the structure failed to be.</summary>
        public WebAuthnParseError Error { get; }

        /// <summary>
        /// Creates a parse exception carrying the error the verifier will translate.
        /// </summary>
        /// <param name="error">What the structure failed to be.</param>
        /// <param name="message">A description of the specific problem, for debugging.</param>
        public WebAuthnParseException(WebAuthnParseError error, string message)
            : base(message)
        {
            Error = error;
        }

        /// <summary>
        /// Creates the exception for a CBOR structure that could not be decoded. Every parser routes its
        /// decoder failures through here, so the three of them cannot drift into describing the same class
        /// of failure differently.
        /// </summary>
        /// <param name="error">What the structure failed to be.</param>
        /// <param name="what">The structure being read, named as it reads in a message, for example <c>"COSE key"</c>.</param>
        /// <param name="exception">The decoder failure.</param>
        public static WebAuthnParseException FromCborFailure(WebAuthnParseError error, string what, Exception exception)
        {
            return new WebAuthnParseException(error, $"{what} is not well-formed CBOR of the expected shape: {exception.Message}");
        }
    }
}
