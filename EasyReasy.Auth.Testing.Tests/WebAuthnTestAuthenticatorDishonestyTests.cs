namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// Each settable value makes the real verifier decline with the reason a consumer's test of its own
    /// enforcement expects, so a consumer setting it is testing the check it names and nothing else.
    /// </summary>
    [TestClass]
    public class WebAuthnTestAuthenticatorDishonestyTests
    {
        [TestMethod]
        public void Assert_NotVerifyingUserWhenRequired_FailsUserNotVerified()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator, WebAuthnUserVerificationRequirement.Required);
            authenticator.VerifiesUser = false;

            WebAuthnAuthenticationResult result = TestRelyingParty.SignInSecondFactor(
                authenticator,
                TestRelyingParty.Stored(credential),
                WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual(WebAuthnAuthenticationFailureReason.UserNotVerified, result.FailureReason);
        }

        [TestMethod]
        public void Register_NotVerifyingUserWhenRequired_AnswersAndFailsUserNotVerified()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.VerifiesUser = false;

            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator().CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Required);
            WebAuthnTestRegistration registration = authenticator.Register(options, TestRelyingParty.Origin);

            WebAuthnRegistrationResult result = TestRelyingParty.NewVerifier().VerifyRegistration(
                registration.Response,
                options.Challenge,
                WebAuthnUserVerificationRequirement.Required);

            Assert.AreEqual(WebAuthnRegistrationFailureReason.UserNotVerified, result.FailureReason);
        }

        [TestMethod]
        public void Assert_NotVerifyingUserWhenOnlyPreferred_SucceedsUnverified()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.VerifiesUser = false;
            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            WebAuthnAuthenticationResult result = TestRelyingParty.SignInSecondFactor(authenticator, TestRelyingParty.Stored(credential));

            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
            Assert.IsFalse(result.UserVerified);
        }

        [TestMethod]
        public void Register_Passkey_RecordsTheUserIdAsTheUserHandle()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            Assert.IsNull(authenticator.UserHandle);

            TestRelyingParty.RegisterPasskey(authenticator);

            CollectionAssert.AreEqual(TestRelyingParty.UserHandle, authenticator.UserHandle);
        }

        [TestMethod]
        public void Assert_AnotherUsersHandle_FailsUserHandleMismatch()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterPasskey(authenticator);
            authenticator.UserHandle = TestRelyingParty.OtherUserHandle;

            WebAuthnAuthenticationResult result = TestRelyingParty.SignInWithPasskey(authenticator, TestRelyingParty.Stored(credential));

            Assert.AreEqual(WebAuthnAuthenticationFailureReason.UserHandleMismatch, result.FailureReason);
        }

        [TestMethod]
        public void Assert_NoUserHandle_FailsUserHandleMissing()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterPasskey(authenticator);
            authenticator.UserHandle = null;

            WebAuthnAuthenticationResult result = TestRelyingParty.SignInWithPasskey(authenticator, TestRelyingParty.Stored(credential));

            Assert.AreEqual(WebAuthnAuthenticationFailureReason.UserHandleMissing, result.FailureReason);
        }

        [TestMethod]
        public void Assert_CounterSetBelowTheStoredOne_FailsSignCounterRegressed()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.SignCount = 10;
            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            // Assert raises the counter before signing, so the assertion reports 5, still below the stored 10.
            authenticator.SignCount = 4;

            WebAuthnAuthenticationResult result = TestRelyingParty.SignInSecondFactor(authenticator, TestRelyingParty.Stored(credential));

            Assert.AreEqual(WebAuthnAuthenticationFailureReason.SignCounterRegressed, result.FailureReason);
        }
    }
}
