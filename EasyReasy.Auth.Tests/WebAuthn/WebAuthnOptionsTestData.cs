namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The generator and the inputs the options tests are written against.
    /// </summary>
    internal static class WebAuthnOptionsTestData
    {
        /// <summary>Base64url of "credential-one" — distinct from every other string in these tests.</summary>
        public const string FirstCredentialId = "Y3JlZGVudGlhbC1vbmU";

        /// <summary>Base64url of "credential-two".</summary>
        public const string SecondCredentialId = "Y3JlZGVudGlhbC10d28";

        /// <summary>The account identifier the test user carries.</summary>
        public const string UserName = "ada@example.com";

        /// <summary>The display name the test user carries, deliberately unlike <see cref="UserName"/>.</summary>
        public const string UserDisplayName = "Ada Lovelace";

        /// <summary>A generator for the relying party the WebAuthn tests share.</summary>
        public static WebAuthnOptionsGenerator NewGenerator()
        {
            return new WebAuthnOptionsGenerator(
                new WebAuthnRelyingParty(WebAuthnTestData.RelyingPartyId, "Contoso", new[] { WebAuthnTestData.Origin }));
        }

        /// <summary>The user the registration tests register a credential for.</summary>
        public static PublicKeyCredentialUserEntity NewUser()
        {
            return new PublicKeyCredentialUserEntity(new byte[] { 0x01, 0x02, 0x03, 0x04 }, UserName, UserDisplayName);
        }
    }
}
