namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// What a <see cref="WebAuthnTestAuthenticator"/> answers an authentication with, in the two forms a
    /// test posts or verifies it in.
    /// </summary>
    public sealed class WebAuthnTestAssertion
    {
        /// <summary>
        /// The body a page posts back: what the browser's <c>PublicKeyCredential.toJSON()</c> writes for an
        /// assertion, including the <c>clientExtensionResults</c> member
        /// <see cref="WebAuthnAuthenticationResponse"/> does not carry.
        /// </summary>
        public string Json { get; }

        /// <summary>
        /// The same assertion as <see cref="WebAuthnAuthenticationResponse.FromJson"/> reads it, for a test
        /// that calls <see cref="WebAuthnVerifier"/> directly.
        /// </summary>
        public WebAuthnAuthenticationResponse Response { get; }

        internal WebAuthnTestAssertion(string json, WebAuthnAuthenticationResponse response)
        {
            Json = json;
            Response = response;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Json;
        }
    }
}
