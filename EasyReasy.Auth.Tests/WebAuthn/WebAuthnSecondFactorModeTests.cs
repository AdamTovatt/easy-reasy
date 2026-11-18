namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The second-factor path reports <see cref="WebAuthnAuthenticationMode.SecondFactor"/> and does not
    /// read the user handle.
    /// </summary>
    [TestClass]
    public class WebAuthnSecondFactorModeTests
    {
        [TestMethod]
        public void VerifyAuthentication_CorrectCeremony_ReportsSecondFactorMode()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            SyntheticAuthenticationCeremony ceremony = new SyntheticAuthenticationCeremony(authenticator, WebAuthnTestData.Challenge);

            WebAuthnAuthenticationResult result = Verify(ceremony);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(WebAuthnAuthenticationMode.SecondFactor, result.Mode);
        }

        [TestMethod]
        public void VerifyAuthentication_FailedCeremony_ReportsSecondFactorMode()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            SyntheticAuthenticationCeremony ceremony = new SyntheticAuthenticationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;

            WebAuthnAuthenticationResult result = Verify(ceremony);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(WebAuthnAuthenticationMode.SecondFactor, result.Mode);
        }

        [TestMethod]
        public void VerifyAuthentication_MalformedStructure_ReportsSecondFactorMode()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            SyntheticAuthenticationCeremony ceremony = new SyntheticAuthenticationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.ClientData.Override = "not json";

            WebAuthnAuthenticationResult result = Verify(ceremony);

            Assert.AreEqual(WebAuthnAuthenticationFailureReason.MalformedClientData, result.FailureReason);
            Assert.AreEqual(WebAuthnAuthenticationMode.SecondFactor, result.Mode);
        }

        [TestMethod]
        public void VerifyAuthentication_NoUserHandle_Succeeds()
        {
            // An authenticator answering a non-discoverable ceremony need not send one.
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            SyntheticAuthenticationCeremony ceremony = new SyntheticAuthenticationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.UserHandle = null;

            WebAuthnAuthenticationResult result = Verify(ceremony);

            Assert.IsTrue(result.Success, result.FailureMessage);
        }

        [TestMethod]
        public void VerifyAuthentication_UserHandleThatMatchesNothing_Succeeds()
        {
            // The first factor already said who the user is; the second factor does not ask again.
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            SyntheticAuthenticationCeremony ceremony = new SyntheticAuthenticationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.UserHandle = Base64UrlEncoding.Encode(new byte[] { 0x0B, 0xAD });

            WebAuthnAuthenticationResult result = Verify(ceremony);

            Assert.IsTrue(result.Success, result.FailureMessage);
        }

        private static WebAuthnAuthenticationResult Verify(SyntheticAuthenticationCeremony ceremony)
        {
            return WebAuthnTestData.NewVerifier().VerifyAuthentication(
                ceremony.Build(),
                WebAuthnTestData.Challenge,
                ceremony.StoredCredential(),
                WebAuthnUserVerificationRequirement.Required);
        }
    }
}
