namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// What a <see cref="WebAuthnTestAuthenticator"/> answers a registration with, in the two forms a test
    /// posts or verifies it in.
    /// </summary>
    public sealed class WebAuthnTestRegistration
    {
        /// <summary>
        /// The body a page posts back: what the browser's <c>PublicKeyCredential.toJSON()</c> writes for a
        /// new credential, including the <c>authenticatorData</c>, <c>publicKey</c>,
        /// <c>publicKeyAlgorithm</c> and <c>clientExtensionResults</c> members
        /// <see cref="WebAuthnRegistrationResponse"/> does not carry.
        /// </summary>
        public string Json { get; }

        /// <summary>
        /// The same registration as <see cref="WebAuthnRegistrationResponse.FromJson"/> reads it, for a
        /// test that calls <see cref="WebAuthnVerifier"/> directly.
        /// </summary>
        public WebAuthnRegistrationResponse Response { get; }

        internal WebAuthnTestRegistration(string json, WebAuthnRegistrationResponse response)
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
