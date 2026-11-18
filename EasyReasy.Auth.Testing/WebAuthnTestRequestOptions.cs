using System.Text.Json;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// The fields of an authentication's request options that the browser and the authenticator act on.
    /// </summary>
    internal sealed class WebAuthnTestRequestOptions
    {
        /// <summary>The relying-party id the assertion is scoped to.</summary>
        public string RelyingPartyId { get; }

        /// <summary>The decoded challenge.</summary>
        public byte[] Challenge { get; }

        /// <summary>
        /// The decoded ids <c>allowCredentials</c> names; empty for a discoverable sign-in, which lets the
        /// authenticator choose.
        /// </summary>
        public IReadOnlyList<byte[]> AllowedCredentialIds { get; }

        private WebAuthnTestRequestOptions(string relyingPartyId, byte[] challenge, IReadOnlyList<byte[]> allowedCredentialIds)
        {
            RelyingPartyId = relyingPartyId;
            Challenge = challenge;
            AllowedCredentialIds = allowedCredentialIds;
        }

        /// <summary>
        /// Reads request options from the JSON an endpoint returned.
        /// </summary>
        /// <param name="optionsJson">The <c>PublicKeyCredentialRequestOptionsJSON</c>.</param>
        /// <param name="origin">
        /// The origin the ceremony runs at, whose host is the relying-party id when the options name none,
        /// as a browser uses it (WebAuthn Level 3 §5.1.4.1 step 8).
        /// </param>
        /// <exception cref="ArgumentException">The JSON lacks a field authentication needs, or a field has the wrong shape.</exception>
        public static WebAuthnTestRequestOptions FromJson(string optionsJson, WebAuthnTestOrigin origin)
        {
            return WebAuthnTestOptionsReader.Read(optionsJson, nameof(optionsJson), (JsonElement root) =>
                new WebAuthnTestRequestOptions(
                    WebAuthnTestOptionsReader.ReadOptionalString(root, "rpId") ?? origin.Host,
                    WebAuthnTestOptionsReader.ReadRequiredBase64Url(root, "challenge"),
                    WebAuthnTestOptionsReader.ReadCredentialIds(root, "allowCredentials")));
        }
    }
}
