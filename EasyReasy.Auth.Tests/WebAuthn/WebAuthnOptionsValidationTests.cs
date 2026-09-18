namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class WebAuthnOptionsValidationTests
    {
        [TestMethod]
        public void Constructor_NullRelyingParty_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new WebAuthnOptionsGenerator(null!));
        }

        [TestMethod]
        public void CreateRegistrationOptions_NullUser_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(null!, WebAuthnUserVerificationRequirement.Required));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void CreateRegistrationOptions_NonPositiveTimeout_Throws(int timeoutMilliseconds)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    WebAuthnUserVerificationRequirement.Required,
                    timeoutMilliseconds: timeoutMilliseconds));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void CreateAuthenticationOptions_NonPositiveTimeout_Throws(int timeoutMilliseconds)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                    new[] { WebAuthnOptionsTestData.FirstCredentialId },
                    WebAuthnUserVerificationRequirement.Required,
                    timeoutMilliseconds: timeoutMilliseconds));
        }

        [TestMethod]
        public void CreateRegistrationOptions_UndefinedUserVerification_Throws()
        {
            // The string enum converter writes an undefined value as a number instead of failing, so
            // without the guard the browser would be handed "userVerification":99.
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    (WebAuthnUserVerificationRequirement)99));

            Assert.AreEqual("userVerification", exception.ParamName);
        }

        [TestMethod]
        public void CreateRegistrationOptions_UndefinedAuthenticatorAttachment_Throws()
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    WebAuthnUserVerificationRequirement.Required,
                    authenticatorAttachment: (WebAuthnAuthenticatorAttachment)99));

            Assert.AreEqual("authenticatorAttachment", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_UndefinedUserVerification_Throws()
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                    new[] { WebAuthnOptionsTestData.FirstCredentialId },
                    (WebAuthnUserVerificationRequirement)99));

            Assert.AreEqual("userVerification", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_NoAllowedCredentials_Throws()
        {
            // An empty allowCredentials asks the browser for any discoverable credential, which is
            // passwordless login — out of scope, and not something verification here could complete.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                    Array.Empty<string>(),
                    WebAuthnUserVerificationRequirement.Required));

            Assert.AreEqual("allowCredentialIds", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_NullAllowedCredentials_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(null!, WebAuthnUserVerificationRequirement.Required));
        }

        [DataTestMethod]
        [DataRow("not base64url!")]
        [DataRow("Y3JlZGVudGlhbC1vbmU=")]   // padded; the wire form is unpadded
        [DataRow("Y3JlZGVudGlh bC1vbmU")]   // embedded whitespace
        [DataRow("Y3JlZGVudGlhbC1vbmU\n")]  // trailing newline
        [DataRow("+/+/")]                   // the standard base64 alphabet
        public void CreateAuthenticationOptions_CredentialIdThatIsNotCanonicalBase64Url_NamesTheArgument(string credentialId)
        {
            // Every one of these would otherwise reach the browser verbatim and produce a ceremony that
            // never matches the credential and never says why.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                    new[] { credentialId },
                    WebAuthnUserVerificationRequirement.Required));

            Assert.AreEqual("allowCredentialIds", exception.ParamName);
        }

        [TestMethod]
        public void CreateRegistrationOptions_ExcludedCredentialThatIsNotCanonicalBase64Url_NamesTheArgument()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    WebAuthnUserVerificationRequirement.Required,
                    excludeCredentialIds: new[] { "Y3JlZGVudGlhbC1vbmU=" }));

            Assert.AreEqual("excludeCredentialIds", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_EmptyCredentialId_NamesTheArgument()
        {
            // The empty string is canonical base64url for zero bytes, so only an explicit guard rejects it.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                    new[] { string.Empty },
                    WebAuthnUserVerificationRequirement.Required));

            Assert.AreEqual("allowCredentialIds", exception.ParamName);
        }

        [TestMethod]
        public void CreateRegistrationOptions_EmptyExcludedCredentialId_NamesTheArgument()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    WebAuthnUserVerificationRequirement.Required,
                    excludeCredentialIds: new[] { string.Empty }));

            Assert.AreEqual("excludeCredentialIds", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_NullCredentialId_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                    new string[] { null! },
                    WebAuthnUserVerificationRequirement.Required));

            Assert.AreEqual("allowCredentialIds", exception.ParamName);
        }

        [TestMethod]
        public void CreateRegistrationOptions_NullExcludedCredentialId_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    WebAuthnUserVerificationRequirement.Required,
                    excludeCredentialIds: new string[] { null! }));

            Assert.AreEqual("excludeCredentialIds", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_BadCredentialId_NamesOnlyTheCallersArgument()
        {
            // The message used to carry two "(Parameter …)" suffixes, the internal one first, because the
            // failure was rewrapped from a constructor deeper down.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnOptionsTestData.NewGenerator().CreateAuthenticationOptions(
                    new[] { "not base64url!" },
                    WebAuthnUserVerificationRequirement.Required));

            Assert.AreEqual(1, exception.Message.Split("(Parameter").Length - 1);
            Assert.IsFalse(exception.Message.Contains("credentialId", StringComparison.Ordinal));
        }
    }
}
