using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// One test per check authentication verification makes, each written by taking an assertion that
    /// verifies and breaking the single field that check reads.
    /// </summary>
    [TestClass]
    public class WebAuthnAuthenticationVerificationTests
    {
        private SyntheticAuthenticator _authenticator = null!;
        private SyntheticAuthenticationCeremony _ceremony = null!;

        /// <summary>
        /// Builds an assertion that verifies. Held as fields so each test below is the one mutation it is
        /// about, with nothing between its name and the field it breaks.
        /// </summary>
        [TestInitialize]
        public void BuildACeremonyThatVerifies()
        {
            _authenticator = SyntheticAuthenticator.Create();
            _ceremony = new SyntheticAuthenticationCeremony(_authenticator, WebAuthnTestData.Challenge);
        }

        [TestCleanup]
        public void DisposeTheAuthenticator()
        {
            _authenticator.Dispose();
        }

        [TestMethod]
        public void VerifyAuthentication_CorrectCeremony_Succeeds()
        {
            WebAuthnAuthenticationResult result = Verify();

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsNull(result.FailureReason);
        }

        [TestMethod]
        public void VerifyAuthentication_Rs256Authenticator_Succeeds()
        {
            // The signature encoding differs between the two algorithms — a DER sequence against PKCS#1 —
            // so a verifier that handled only one would pass every test above and fail here.
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(CoseAlgorithm.Rs256);
            SyntheticAuthenticationCeremony ceremony = new SyntheticAuthenticationCeremony(authenticator, WebAuthnTestData.Challenge);

            WebAuthnAuthenticationResult result = Verify(ceremony);

            Assert.IsTrue(result.Success, result.FailureMessage);
        }

        [TestMethod]
        public void VerifyAuthentication_SignatureOverDifferentBytes_IsRejected()
        {
            // Correctly formed and correctly signed — over something other than what was sent. Without
            // this, a verifier that checked the signature against bytes it assembled differently would
            // still pass every other test here.
            _ceremony.SignedBytesOverride = Encoding.UTF8.GetBytes("some other bytes entirely");

            AssertFailure(WebAuthnAuthenticationFailureReason.SignatureInvalid);
        }

        [TestMethod]
        public void VerifyAuthentication_SignatureFromAnotherAuthenticator_IsRejected()
        {
            using SyntheticAuthenticator other = SyntheticAuthenticator.Create();
            _ceremony.SignatureOverride = other.Sign(Encoding.UTF8.GetBytes("anything"));

            AssertFailure(WebAuthnAuthenticationFailureReason.SignatureInvalid);
        }

        [TestMethod]
        public void VerifyAuthentication_ClientDataRewrittenAfterSigning_IsRejected()
        {
            // Client data carrying every field the earlier checks read, with the same values, in bytes
            // other than the ones that were signed — the shape a verifier would accept if it hashed client
            // data it had re-serialized from the fields it parsed rather than the bytes as received. Every
            // check before the signature passes on this, so the rejection can only come from the signature.
            _ceremony.ClientDataJsonSentInstead = $"{{\"type\":\"{CollectedClientData.AuthenticationCeremonyType}\"," +
                $"\"challenge\":\"{WebAuthnTestData.Challenge}\"," +
                $"\"origin\":\"{WebAuthnTestData.Origin}\"," +
                "\"crossOrigin\":false," +
                "\"extraFieldABrowserMayAdd\":\"changes the bytes and nothing else\"}";

            AssertFailure(WebAuthnAuthenticationFailureReason.SignatureInvalid);
        }

        [TestMethod]
        public void VerifyAuthentication_AssertionForAnotherCredential_IsRejected()
        {
            _ceremony.ReportedCredentialId = Base64UrlEncoding.Encode(new byte[] { 0xAA, 0xBB, 0xCC });

            AssertFailure(WebAuthnAuthenticationFailureReason.CredentialIdMismatch);
        }

        [TestMethod]
        public void VerifyAuthentication_WrongCeremonyType_IsRejected()
        {
            _ceremony.ClientData.CeremonyType = CollectedClientData.RegistrationCeremonyType;

            AssertFailure(WebAuthnAuthenticationFailureReason.WrongCeremonyType);
        }

        [TestMethod]
        public void VerifyAuthentication_ChallengeFromAnotherCeremony_IsRejected()
        {
            _ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;

            AssertFailure(WebAuthnAuthenticationFailureReason.ChallengeMismatch);
        }

        [TestMethod]
        public void VerifyAuthentication_PaddedChallenge_IsAccepted()
        {
            // The browser writes unpadded base64url, but a client library that pads it has still named the
            // bytes that were issued. The strict rule is for the challenge the application stored, not for
            // the one the browser recorded, and this is what pins which of the two decoders this path uses.
            _ceremony.ClientData.Challenge = WebAuthnTestData.Challenge + "=";

            Assert.IsTrue(Verify().Success);
        }

        [TestMethod]
        public void VerifyAuthentication_OriginOutsideTheConfiguredSet_IsRejected()
        {
            _ceremony.ClientData.Origin = "https://evil.example.com";

            AssertFailure(WebAuthnAuthenticationFailureReason.OriginNotAllowed);
        }

        [TestMethod]
        public void VerifyAuthentication_CrossOriginCeremony_IsRejected()
        {
            _ceremony.ClientData.CrossOrigin = true;

            AssertFailure(WebAuthnAuthenticationFailureReason.CrossOriginCeremony);
        }

        [TestMethod]
        public void VerifyAuthentication_AssertionScopedToAnotherRelyingParty_IsRejected()
        {
            _ceremony.AuthenticatorData.RelyingPartyIdHash = SHA256.HashData(Encoding.UTF8.GetBytes("other.example.com"));

            AssertFailure(WebAuthnAuthenticationFailureReason.RelyingPartyIdHashMismatch);
        }

        [TestMethod]
        public void VerifyAuthentication_UserPresenceNotReported_IsRejected()
        {
            _ceremony.AuthenticatorData.UserPresent = false;

            AssertFailure(WebAuthnAuthenticationFailureReason.UserNotPresent);
        }

        [TestMethod]
        public void VerifyAuthentication_UserVerificationRequiredAndNotReported_IsRejected()
        {
            _ceremony.AuthenticatorData.UserVerified = false;

            AssertFailure(WebAuthnAuthenticationFailureReason.UserNotVerified);
        }

        [DataTestMethod]
        [DataRow(WebAuthnUserVerificationRequirement.Preferred)]
        [DataRow(WebAuthnUserVerificationRequirement.Discouraged)]
        public void VerifyAuthentication_UserVerificationNotRequiredAndNotReported_IsAccepted(WebAuthnUserVerificationRequirement userVerification)
        {
            _ceremony.AuthenticatorData.UserVerified = false;

            WebAuthnAuthenticationResult result = Verify(_ceremony, userVerification: userVerification);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsFalse(result.UserVerified);
        }

        [TestMethod]
        public void VerifyAuthentication_UserVerificationReported_IsReportedOnTheResult()
        {
            WebAuthnAuthenticationResult result = Verify(_ceremony, userVerification: WebAuthnUserVerificationRequirement.Discouraged);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsTrue(result.UserVerified);
        }

        [TestMethod]
        public void VerifyAuthentication_BackedUpCredential_IsReportedOnTheResult()
        {
            // Unlike backup eligibility this can change over a credential's life, which is why it is
            // reported on every assertion rather than only at registration.
            _ceremony.AuthenticatorData.BackupEligible = true;
            _ceremony.AuthenticatorData.BackedUp = true;

            WebAuthnAuthenticationResult result = Verify();

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsTrue(result.BackedUp);
        }

        [TestMethod]
        public void VerifyAuthentication_CredentialThatIsNotBackedUp_IsReportedOnTheResult()
        {
            WebAuthnAuthenticationResult result = Verify();

            // Asserted against a ceremony that succeeded, because a declined one reports false here too —
            // without this the test would stay green if the assertion stopped verifying altogether.
            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsFalse(result.BackedUp);
        }

        [TestMethod]
        public void VerifyAuthentication_BackupEligibleButNotBackedUp_ReportsTheBackedUpFlagAndNotTheOther()
        {
            // The two tests above leave the pair of flags equal, so neither could catch the result being
            // filled from backup eligibility instead. This is the only combination where they differ —
            // backed up without being eligible is rejected before it gets here.
            _ceremony.AuthenticatorData.BackupEligible = true;
            _ceremony.AuthenticatorData.BackedUp = false;

            WebAuthnAuthenticationResult result = Verify();

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsFalse(result.BackedUp);
        }

        [TestMethod]
        public void VerifyAuthentication_ClientDataThatIsNotJson_IsRejected()
        {
            _ceremony.ClientData.Override = "not json";

            AssertFailure(WebAuthnAuthenticationFailureReason.MalformedClientData);
        }

        [TestMethod]
        public void VerifyAuthentication_TruncatedAuthenticatorData_IsRejected()
        {
            // Signed over correctly, so the failure is the structure rather than the signature.
            _ceremony.AuthenticatorData.RelyingPartyIdHash = new byte[] { 0x01, 0x02, 0x03 };

            AssertFailure(WebAuthnAuthenticationFailureReason.MalformedAuthenticatorData);
        }

        [TestMethod]
        public void VerifyAuthentication_StoredPublicKeyThatIsNotAWellFormedCoseKey_IsRejected()
        {
            WebAuthnStoredCredential credential = new WebAuthnStoredCredential(
                Base64UrlEncoding.Encode(_authenticator.CredentialId),
                Base64UrlEncoding.Encode(CborTestEncoder.EncodeCoseEc2Key(new byte[31], new byte[32])),
                0);

            AssertFailure(WebAuthnAuthenticationFailureReason.MalformedStoredCredential, credential);
        }

        [TestMethod]
        public void VerifyAuthentication_StoredPublicKeyStatingAnUnsupportedAlgorithm_IsRejected()
        {
            // At registration this is its own reason, because there it describes what an authenticator just
            // offered. Here the key came from the application's own store, so both readings are the same
            // thing: what was handed back is not a credential this library produced.
            WebAuthnStoredCredential credential = new WebAuthnStoredCredential(
                Base64UrlEncoding.Encode(_authenticator.CredentialId),
                Base64UrlEncoding.Encode(CborTestEncoder.EncodeCoseEc2Key(new byte[32], new byte[32], algorithm: -35)),
                0);

            AssertFailure(WebAuthnAuthenticationFailureReason.MalformedStoredCredential, credential);
        }

        [TestMethod]
        public void VerifyAuthentication_FailedCeremony_ReportsNoCounterNoFlagsAndNoCounterVerdict()
        {
            // The values below are the ones a verified assertion would have reported, so this shows the
            // failure path leaving them unset rather than merely agreeing with the defaults: an assertion
            // built to report a counter of 42 and a backed-up credential reports neither once declined.
            _ceremony.AuthenticatorData.SignCount = 42;
            _ceremony.AuthenticatorData.BackupEligible = true;
            _ceremony.AuthenticatorData.BackedUp = true;
            _ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;

            WebAuthnAuthenticationResult result = Verify(storedCredential: _ceremony.StoredCredential(7));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0u, result.SignCount);
            Assert.IsNull(result.SignCounterState);
            Assert.IsFalse(result.UserVerified);
            Assert.IsFalse(result.BackedUp);
            Assert.IsNotNull(result.FailureMessage);
        }

        [TestMethod]
        public void VerifyAuthentication_ResponseParsedFromTheBrowsersJson_Succeeds()
        {
            // Every other test here hands the verifier an object built in memory. This is the one place the
            // JSON contract and verification meet: a real assertion serialized the way a browser serializes
            // it, read back through the public entry point, and verified.
            _ceremony.UserHandle = Base64UrlEncoding.Encode(Encoding.UTF8.GetBytes("user-handle"));

            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(_ceremony.BuildJson());

            WebAuthnAuthenticationResult result = WebAuthnTestData.NewVerifier().VerifyAuthentication(
                response,
                WebAuthnTestData.Challenge,
                _ceremony.StoredCredential(),
                WebAuthnUserVerificationRequirement.Required);

            Assert.IsTrue(result.Success, result.FailureMessage);
        }

        private WebAuthnAuthenticationResult Verify(
            SyntheticAuthenticationCeremony? ceremony = null,
            WebAuthnStoredCredential? storedCredential = null,
            WebAuthnUserVerificationRequirement userVerification = WebAuthnUserVerificationRequirement.Required)
        {
            SyntheticAuthenticationCeremony effectiveCeremony = ceremony ?? _ceremony;

            return WebAuthnTestData.NewVerifier().VerifyAuthentication(
                effectiveCeremony.Build(),
                WebAuthnTestData.Challenge,
                storedCredential ?? effectiveCeremony.StoredCredential(),
                userVerification);
        }

        private void AssertFailure(WebAuthnAuthenticationFailureReason expectedReason, WebAuthnStoredCredential? storedCredential = null)
        {
            WebAuthnAuthenticationResult result = Verify(storedCredential: storedCredential);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(expectedReason, result.FailureReason);
        }
    }
}
