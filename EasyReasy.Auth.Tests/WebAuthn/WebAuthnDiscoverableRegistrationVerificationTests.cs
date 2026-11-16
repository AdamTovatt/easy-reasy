namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// <see cref="WebAuthnVerifier.VerifyDiscoverableRegistration"/>: the registration checks with user
    /// verification fixed at required, against a real authenticator.
    /// </summary>
    [TestClass]
    public class WebAuthnDiscoverableRegistrationVerificationTests
    {
        [DataTestMethod]
        [DataRow(CoseAlgorithm.Es256)]
        [DataRow(CoseAlgorithm.Rs256)]
        public void VerifyDiscoverableRegistration_CorrectCeremony_ReportsTheCredentialToStore(CoseAlgorithm algorithm)
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(algorithm);
            SyntheticRegistrationCeremony ceremony = new SyntheticRegistrationCeremony(authenticator, WebAuthnTestData.Challenge);

            WebAuthnRegistrationResult result = Verify(ceremony);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsNotNull(result.Credential);
            Assert.AreEqual(Base64UrlEncoding.Encode(authenticator.CredentialId), result.Credential.CredentialId);
            Assert.AreEqual(algorithm, result.Credential.Algorithm);
            Assert.IsTrue(result.UserVerified);
        }

        [TestMethod]
        public void VerifyDiscoverableRegistration_UserVerificationNotReported_IsRejected()
        {
            // The passkey is the whole login, so a touch without verification must not enroll one. A
            // verify call that took the requirement from anywhere but Required would accept this.
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            SyntheticRegistrationCeremony ceremony = new SyntheticRegistrationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.AuthenticatorData.UserVerified = false;

            WebAuthnRegistrationResult result = Verify(ceremony);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(WebAuthnRegistrationFailureReason.UserNotVerified, result.FailureReason);
        }

        [TestMethod]
        public void VerifyDiscoverableRegistration_ChallengeFromAnotherCeremony_IsRejected()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            SyntheticRegistrationCeremony ceremony = new SyntheticRegistrationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;

            WebAuthnRegistrationResult result = Verify(ceremony);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(WebAuthnRegistrationFailureReason.ChallengeMismatch, result.FailureReason);
        }

        private static WebAuthnRegistrationResult Verify(SyntheticRegistrationCeremony ceremony)
        {
            return WebAuthnTestData.NewVerifier().VerifyDiscoverableRegistration(ceremony.Build(), WebAuthnTestData.Challenge);
        }
    }
}
