namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The inputs the options tests are written against. The relying party and the subjects built from it
    /// are shared, and live in <see cref="WebAuthnTestData"/>; what is here is specific to generating
    /// options.
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

        /// <summary>The user the registration tests register a credential for.</summary>
        public static PublicKeyCredentialUserEntity NewUser()
        {
            return new PublicKeyCredentialUserEntity(new byte[] { 0x01, 0x02, 0x03, 0x04 }, UserName, UserDisplayName);
        }
    }
}
