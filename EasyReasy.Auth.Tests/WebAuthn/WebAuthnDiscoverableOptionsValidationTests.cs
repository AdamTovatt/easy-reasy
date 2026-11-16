namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the passkey sign-in options methods refuse.
    /// </summary>
    [TestClass]
    public class WebAuthnDiscoverableOptionsValidationTests
    {
        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_NullUser_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(null!, Array.Empty<WebAuthnCredentialReference>()));

            Assert.AreEqual("user", exception.ParamName);
        }

        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_NullExcludedCredentials_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(WebAuthnOptionsTestData.NewUser(), null!));

            Assert.AreEqual("excludeCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_NullExcludedCredential_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    new WebAuthnCredentialReference[] { null! }));

            Assert.AreEqual("excludeCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_UndefinedAttachment_Throws()
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    Array.Empty<WebAuthnCredentialReference>(),
                    (WebAuthnAuthenticatorAttachment)99));

            Assert.AreEqual("authenticatorAttachment", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void CreateDiscoverableRegistrationOptions_TimeoutThatIsNotPositive_Throws(int timeoutMilliseconds)
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    Array.Empty<WebAuthnCredentialReference>(),
                    timeoutMilliseconds: timeoutMilliseconds));

            Assert.AreEqual("timeoutMilliseconds", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void CreateDiscoverableAuthenticationOptions_TimeoutThatIsNotPositive_Throws(int timeoutMilliseconds)
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnTestData.NewGenerator().CreateDiscoverableAuthenticationOptions(timeoutMilliseconds));

            Assert.AreEqual("timeoutMilliseconds", exception.ParamName);
        }

        [TestMethod]
        public void CreateDiscoverableAuthenticationOptions_EachCall_IssuesAFreshChallenge()
        {
            WebAuthnOptionsGenerator generator = WebAuthnTestData.NewGenerator();

            Assert.AreNotEqual(
                generator.CreateDiscoverableAuthenticationOptions().Challenge,
                generator.CreateDiscoverableAuthenticationOptions().Challenge);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("not base64url!")]
        public void CreateDiscoverableRegistrationOptions_ExcludedCredentialIdThatIsNotCanonical_NamesTheArgument(string credentialId)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    new[] { new WebAuthnCredentialReference(credentialId) }));

            Assert.AreEqual("excludeCredentials", exception.ParamName);
        }
    }
}
