namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the stored credential refuses to be constructed with. Both encoded fields came out of
    /// registration in canonical base64url, so anything else is a value that did not survive storage — and
    /// saying so here is what keeps a truncated column from being reported later as a bad signature.
    /// </summary>
    [TestClass]
    public class WebAuthnStoredCredentialTests
    {
        private const string CredentialId = WebAuthnResponseTestData.CredentialId;

        /// <summary>Base64url of "public-key" — distinct from the credential id, and not a prefix of it.</summary>
        private const string PublicKey = "cHVibGljLWtleQ";

        [TestMethod]
        public void Constructor_WhatRegistrationReported_KeepsBothValues()
        {
            // Distinct values, neither a prefix of the other, so a constructor assigning one from the other
            // fails here.
            WebAuthnStoredCredential credential = new WebAuthnStoredCredential(CredentialId, PublicKey, 7);

            Assert.AreEqual(CredentialId, credential.CredentialId);
            Assert.AreEqual(PublicKey, credential.PublicKey);
            Assert.AreEqual(7u, credential.SignCount);
        }

        [DataTestMethod]
        [DataRow("credentialId")]
        [DataRow("publicKey")]
        public void Constructor_ValueThatIsNotBase64Url_NamesIt(string badField)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewCredential(badField, "not base64url!"));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("credentialId")]
        [DataRow("publicKey")]
        public void Constructor_PaddedValue_NamesIt(string badField)
        {
            // Padding is valid base64url of the right bytes, so only re-encoding catches it. It means the
            // value was re-encoded somewhere between being reported and being handed back, which is worth
            // knowing before a ceremony fails for a reason that sounds like an attack.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewCredential(badField, CredentialId + "="));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("credentialId")]
        [DataRow("publicKey")]
        public void Constructor_StandardBase64Alphabet_NamesIt(string badField)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewCredential(badField, "+/+/"));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("credentialId")]
        [DataRow("publicKey")]
        public void Constructor_ValueStandingForNoBytes_NamesIt(string badField)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewCredential(badField, string.Empty));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("credentialId")]
        [DataRow("publicKey")]
        public void Constructor_NullArgument_NamesIt(string nullArgumentName)
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => NewCredential(nullArgumentName, null!));

            Assert.AreEqual(nullArgumentName, exception.ParamName);
        }

        [TestMethod]
        public void Constructor_WhatRegistrationActuallyReturns_IsAccepted()
        {
            // The real round trip rather than a hand-written pair: whatever registration reports has to be
            // constructible here, or the two halves of the feature do not join up.
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            SyntheticRegistrationCeremony ceremony = new SyntheticRegistrationCeremony(authenticator, WebAuthnTestData.Challenge);

            WebAuthnRegisteredCredential registered = WebAuthnTestData.NewVerifier().VerifyRegistration(
                ceremony.Build(),
                WebAuthnTestData.Challenge,
                WebAuthnUserVerificationRequirement.Required).Credential!;

            WebAuthnStoredCredential stored = new WebAuthnStoredCredential(registered.CredentialId, registered.PublicKey, registered.SignCount);

            Assert.AreEqual(registered.CredentialId, stored.CredentialId);
            Assert.AreEqual(registered.PublicKey, stored.PublicKey);
        }

        private static WebAuthnStoredCredential NewCredential(string fieldName, string value)
        {
            return new WebAuthnStoredCredential(
                fieldName == "credentialId" ? value : CredentialId,
                fieldName == "publicKey" ? value : PublicKey,
                0);
        }
    }
}
