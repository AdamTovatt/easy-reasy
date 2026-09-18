namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the verifier refuses to be called with. These are the failures that are the application's
    /// mistake rather than the ceremony's, and they are exceptions for exactly that reason — a caller who
    /// lost the challenge should not see the same shape of answer as a user whose security key was
    /// declined.
    /// </summary>
    [TestClass]
    public class WebAuthnVerifierArgumentTests
    {
        [TestMethod]
        public void Constructor_NullRelyingParty_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new WebAuthnVerifier(null!));
        }

        [TestMethod]
        public void VerifyRegistration_NullResponse_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnTestData.NewVerifier().VerifyRegistration(
                    null!,
                    WebAuthnTestData.Challenge,
                    WebAuthnUserVerificationRequirement.Required));
        }

        [TestMethod]
        public void VerifyRegistration_NullChallenge_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnTestData.NewVerifier().VerifyRegistration(
                    NewResponse(authenticator),
                    null!,
                    WebAuthnUserVerificationRequirement.Required));
        }

        [TestMethod]
        public void VerifyRegistration_PaddedStoredChallenge_Throws()
        {
            // The one row that reaches the canonicality comparison rather than the validity gate in front
            // of it: padding is valid base64url of the right bytes, so only re-encoding catches it. Without
            // this the whole canonical rule could be replaced by a plain decode and nothing would fail.
            AssertChallengeRejected(WebAuthnTestData.Challenge + "=");
        }

        [TestMethod]
        public void VerifyRegistration_TruncatedStoredChallenge_Throws()
        {
            // Canonical base64url of 30 bytes rather than 32 — a challenge shortened in storage or in
            // transit. Encoding alone cannot tell this from a challenge that was issued, so the length is
            // checked too; otherwise every ceremony fails as a mismatch and reports an attack per attempt
            // instead of the one storage bug behind all of them.
            AssertChallengeRejected(WebAuthnTestData.Challenge.Substring(0, 40));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("not base64url!")]
        [DataRow("+/+/")]            // the standard base64 alphabet
        [DataRow("Y2hhbGxlbmdl")]    // canonical base64url, but of nine bytes
        public void VerifyRegistration_ChallengeThatWasNotIssuedInThisEncoding_Throws(string challenge)
        {
            // A stored challenge that is not what was handed out is the application having mangled it, and
            // every ceremony would fail on it. Reported as a mismatch it would read as an attack in
            // progress rather than as the bug it is.
            AssertChallengeRejected(challenge);
        }

        private static void AssertChallengeRejected(string challenge)
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnTestData.NewVerifier().VerifyRegistration(
                    NewResponse(authenticator),
                    challenge,
                    WebAuthnUserVerificationRequirement.Required));

            Assert.AreEqual("expectedChallenge", exception.ParamName);
        }

        [TestMethod]
        public void VerifyRegistration_UndefinedUserVerification_Throws()
        {
            // An undefined value compares unequal to Required, so without the guard it would verify as the
            // weakest setting — a cast turning a policy off instead of failing.
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnTestData.NewVerifier().VerifyRegistration(
                    NewResponse(authenticator),
                    WebAuthnTestData.Challenge,
                    (WebAuthnUserVerificationRequirement)99));

            Assert.AreEqual("userVerification", exception.ParamName);
        }

        private static WebAuthnRegistrationResponse NewResponse(SyntheticAuthenticator authenticator)
        {
            return new SyntheticRegistrationCeremony(authenticator, WebAuthnTestData.Challenge).Build();
        }
    }
}
