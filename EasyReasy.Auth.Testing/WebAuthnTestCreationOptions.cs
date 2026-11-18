using System.Text.Json;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// The fields of a registration's creation options that the browser and the authenticator act on.
    /// </summary>
    internal sealed class WebAuthnTestCreationOptions
    {
        /// <summary>The relying-party id the credential is scoped to.</summary>
        public string RelyingPartyId { get; }

        /// <summary>The decoded <c>user.id</c>, the user handle the credential stores.</summary>
        public byte[] UserHandle { get; }

        /// <summary>The decoded challenge.</summary>
        public byte[] Challenge { get; }

        /// <summary>The COSE algorithms <c>pubKeyCredParams</c> offers.</summary>
        public IReadOnlyList<int> Algorithms { get; }

        /// <summary>The decoded ids <c>excludeCredentials</c> names.</summary>
        public IReadOnlyList<byte[]> ExcludedCredentialIds { get; }

        private WebAuthnTestCreationOptions(
            string relyingPartyId,
            byte[] userHandle,
            byte[] challenge,
            IReadOnlyList<int> algorithms,
            IReadOnlyList<byte[]> excludedCredentialIds)
        {
            RelyingPartyId = relyingPartyId;
            UserHandle = userHandle;
            Challenge = challenge;
            Algorithms = algorithms;
            ExcludedCredentialIds = excludedCredentialIds;
        }

        /// <summary>
        /// Reads creation options from the JSON an endpoint returned.
        /// </summary>
        /// <param name="optionsJson">The <c>PublicKeyCredentialCreationOptionsJSON</c>.</param>
        /// <param name="origin">
        /// The origin the ceremony runs at, whose host is the relying-party id when the options name none,
        /// as a browser uses it (WebAuthn Level 3 §5.1.3 step 8).
        /// </param>
        /// <exception cref="ArgumentException">The JSON lacks a field registration needs, or a field has the wrong shape.</exception>
        public static WebAuthnTestCreationOptions FromJson(string optionsJson, WebAuthnTestOrigin origin)
        {
            return WebAuthnTestOptionsReader.Read(optionsJson, nameof(optionsJson), (JsonElement root) =>
            {
                JsonElement relyingParty = WebAuthnTestOptionsReader.ReadRequiredObject(root, "rp");
                JsonElement user = WebAuthnTestOptionsReader.ReadRequiredObject(root, "user");

                return new WebAuthnTestCreationOptions(
                    WebAuthnTestOptionsReader.ReadOptionalString(relyingParty, "id") ?? origin.Host,
                    WebAuthnTestOptionsReader.ReadRequiredBase64Url(user, "id"),
                    WebAuthnTestOptionsReader.ReadRequiredBase64Url(root, "challenge"),
                    WebAuthnTestOptionsReader.ReadAlgorithms(root, "pubKeyCredParams"),
                    WebAuthnTestOptionsReader.ReadCredentialIds(root, "excludeCredentials"));
            });
        }
    }
}
