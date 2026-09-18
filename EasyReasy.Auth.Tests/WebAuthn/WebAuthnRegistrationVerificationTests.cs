using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// One test per check registration verification makes, each written by taking a ceremony that verifies
    /// and breaking the single field that check reads.
    /// </summary>
    [TestClass]
    public class WebAuthnRegistrationVerificationTests
    {
        private SyntheticAuthenticator _authenticator = null!;
        private SyntheticRegistrationCeremony _ceremony = null!;

        /// <summary>
        /// Builds a ceremony that verifies. Held as fields so each test below is the one mutation it is
        /// about, with nothing between its name and the field it breaks.
        /// </summary>
        [TestInitialize]
        public void BuildACeremonyThatVerifies()
        {
            _authenticator = SyntheticAuthenticator.Create();
            _ceremony = new SyntheticRegistrationCeremony(_authenticator, WebAuthnTestData.Challenge);
        }

        [TestCleanup]
        public void DisposeTheAuthenticator()
        {
            _authenticator.Dispose();
        }

        [TestMethod]
        public void VerifyRegistration_CorrectCeremony_Succeeds()
        {
            WebAuthnRegistrationResult result = Verify(_ceremony);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsNull(result.FailureReason);
        }

        [TestMethod]
        public void VerifyRegistration_CorrectCeremony_ReportsTheCredentialToStore()
        {
            _ceremony.AuthenticatorData.SignCount = 7;

            WebAuthnRegisteredCredential credential = Verify(_ceremony).Credential!;

            Assert.AreEqual(Base64UrlEncoding.Encode(_authenticator.CredentialId), credential.CredentialId);
            Assert.AreEqual(Base64UrlEncoding.Encode(_authenticator.EncodeCoseKey()), credential.PublicKey);
            Assert.AreEqual(CoseAlgorithm.Es256, credential.Algorithm);
            Assert.AreEqual(7u, credential.SignCount);
            Assert.AreEqual(SyntheticAuthenticator.SyntheticAaguid, credential.Aaguid);
        }

        [TestMethod]
        public void VerifyRegistration_Rs256Authenticator_StoresTheRsaAlgorithm()
        {
            // The algorithm is read out of the key rather than assumed, so both supported ones have to
            // reach the stored credential.
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(CoseAlgorithm.Rs256);
            SyntheticRegistrationCeremony ceremony = new SyntheticRegistrationCeremony(authenticator, WebAuthnTestData.Challenge);

            WebAuthnRegisteredCredential credential = Verify(ceremony).Credential!;

            Assert.AreEqual(CoseAlgorithm.Rs256, credential.Algorithm);
        }

        [TestMethod]
        public void VerifyRegistration_BackupFlags_ReachTheStoredCredential()
        {
            _ceremony.AuthenticatorData.BackupEligible = true;
            _ceremony.AuthenticatorData.BackedUp = true;

            WebAuthnRegisteredCredential credential = Verify(_ceremony).Credential!;

            Assert.IsTrue(credential.BackupEligible);
            Assert.IsTrue(credential.BackedUp);
        }

        [TestMethod]
        public void VerifyRegistration_ClearedBackupFlags_ReachTheStoredCredential()
        {
            // The test above cannot tell the flags being read from them being hard-coded true; this one,
            // against the builder's false defaults, is what makes both pairs of assertions bite.
            WebAuthnRegisteredCredential credential = Verify(_ceremony).Credential!;

            Assert.IsFalse(credential.BackupEligible);
            Assert.IsFalse(credential.BackedUp);
        }

        [TestMethod]
        public void VerifyRegistration_BackupEligibleButNotBackedUp_KeepsTheTwoFlagsApart()
        {
            // The two tests above set both flags the same way, so neither could catch the pair being
            // swapped on the way into the credential. This is the only asymmetric combination an
            // authenticator may report — backed up without being eligible is rejected before it gets here —
            // which makes it the one that pins the order.
            _ceremony.AuthenticatorData.BackupEligible = true;
            _ceremony.AuthenticatorData.BackedUp = false;

            WebAuthnRegisteredCredential credential = Verify(_ceremony).Credential!;

            Assert.IsTrue(credential.BackupEligible);
            Assert.IsFalse(credential.BackedUp);
        }

        [TestMethod]
        public void VerifyRegistration_Transports_ReachTheStoredCredential()
        {
            // Reported by no other ceremony, so an application that does not take them from here cannot
            // recover them without re-enrolling the authenticator.
            _ceremony.Transports = new[] { "internal", "hybrid" };

            WebAuthnRegisteredCredential credential = Verify(_ceremony).Credential!;

            CollectionAssert.AreEqual(new[] { "internal", "hybrid" }, credential.Transports!.ToArray());
        }

        [TestMethod]
        public void VerifyRegistration_NoTransports_LeavesThemNull()
        {
            Assert.IsNull(Verify(_ceremony).Credential!.Transports);
        }

        [TestMethod]
        public void VerifyRegistration_WrongCeremonyType_IsRejected()
        {
            _ceremony.ClientData.CeremonyType = CollectedClientData.AuthenticationCeremonyType;

            AssertFailure(WebAuthnRegistrationFailureReason.WrongCeremonyType, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_ChallengeFromAnotherCeremony_IsRejected()
        {
            _ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;

            AssertFailure(WebAuthnRegistrationFailureReason.ChallengeMismatch, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_ChallengeThatIsNotBase64Url_IsRejectedAsAMismatch()
        {
            _ceremony.ClientData.Challenge = "not base64url!";

            AssertFailure(WebAuthnRegistrationFailureReason.ChallengeMismatch, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_PaddedChallenge_IsAccepted()
        {
            // The browser writes unpadded base64url, but a client library that pads it has still named the
            // bytes that were issued, and failing the ceremony over a spelling would be wrong.
            _ceremony.ClientData.Challenge = WebAuthnTestData.Challenge + "=";

            AssertSucceeds(_ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_OriginOutsideTheConfiguredSet_IsRejected()
        {
            _ceremony.ClientData.Origin = "https://evil.example.com";

            AssertFailure(WebAuthnRegistrationFailureReason.OriginNotAllowed, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_OriginOnTheWrongScheme_IsRejected()
        {
            // Same host and port, different scheme: a different origin, and the one a page served over a
            // stripped connection would report.
            _ceremony.ClientData.Origin = "http://example.com";

            AssertFailure(WebAuthnRegistrationFailureReason.OriginNotAllowed, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_CrossOriginCeremony_IsRejected()
        {
            _ceremony.ClientData.CrossOrigin = true;

            AssertFailure(WebAuthnRegistrationFailureReason.CrossOriginCeremony, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_NoCrossOriginField_IsAccepted()
        {
            _ceremony.ClientData.CrossOrigin = null;

            AssertSucceeds(_ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_AuthenticatorScopedToAnotherRelyingParty_IsRejected()
        {
            _ceremony.AuthenticatorData.RelyingPartyIdHash = SHA256.HashData(Encoding.UTF8.GetBytes("other.example.com"));

            AssertFailure(WebAuthnRegistrationFailureReason.RelyingPartyIdHashMismatch, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_UserPresenceNotReported_IsRejected()
        {
            _ceremony.AuthenticatorData.UserPresent = false;

            AssertFailure(WebAuthnRegistrationFailureReason.UserNotPresent, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_UserVerificationRequiredAndNotReported_IsRejected()
        {
            _ceremony.AuthenticatorData.UserVerified = false;

            AssertFailure(WebAuthnRegistrationFailureReason.UserNotVerified, _ceremony);
        }

        [DataTestMethod]
        [DataRow(WebAuthnUserVerificationRequirement.Preferred)]
        [DataRow(WebAuthnUserVerificationRequirement.Discouraged)]
        public void VerifyRegistration_UserVerificationNotRequiredAndNotReported_IsAccepted(WebAuthnUserVerificationRequirement userVerification)
        {
            // Under anything but Required an authenticator that cannot verify the user is expected to
            // complete the ceremony on presence alone, so rejecting here would lock out exactly the
            // authenticators the setting exists to accommodate.
            _ceremony.AuthenticatorData.UserVerified = false;

            WebAuthnRegistrationResult result = Verify(_ceremony, userVerification);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsFalse(result.UserVerified);
        }

        [TestMethod]
        public void VerifyRegistration_UserVerificationReported_IsReportedOnTheResult()
        {
            // Under Discouraged nothing enforces the flag, so a result that merely echoed the requirement
            // would say false here.

            Assert.IsTrue(Verify(_ceremony, WebAuthnUserVerificationRequirement.Discouraged).UserVerified);
        }

        [TestMethod]
        public void VerifyRegistration_NoAttestedCredentialData_IsRejected()
        {
            _ceremony.AuthenticatorData.Aaguid = null;
            _ceremony.AuthenticatorData.CredentialId = null;
            _ceremony.AuthenticatorData.CredentialPublicKey = null;
            _ceremony.ReportedCredentialId = Base64UrlEncoding.Encode(_authenticator.CredentialId);

            AssertFailure(WebAuthnRegistrationFailureReason.MissingAttestedCredentialData, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_CredentialIdTheBrowserDidNotGetFromTheAuthenticator_IsRejected()
        {
            _ceremony.ReportedCredentialId = Base64UrlEncoding.Encode(new byte[] { 0xAA, 0xBB, 0xCC });

            AssertFailure(WebAuthnRegistrationFailureReason.CredentialIdMismatch, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_AttestationFormatThisLibraryDoesNotVerify_IsRejected()
        {
            _ceremony.AttestationFormat = "packed";

            AssertFailure(WebAuthnRegistrationFailureReason.UnsupportedAttestationFormat, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_AttestationObjectThatIsNotCbor_IsRejected()
        {
            _ceremony.AttestationObjectOverride = new byte[] { 0xFF, 0xFF, 0xFF };

            AssertFailure(WebAuthnRegistrationFailureReason.MalformedAttestationObject, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_TruncatedAuthenticatorData_IsRejected()
        {
            _ceremony.AttestationObjectOverride = CborTestEncoder.EncodeAttestationObject(
                AttestationObject.NoneFormat,
                new byte[] { 0x01, 0x02, 0x03 },
                CborTestEncoder.EncodeEmptyMap());

            AssertFailure(WebAuthnRegistrationFailureReason.MalformedAuthenticatorData, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_ClientDataThatIsNotJson_IsRejected()
        {
            _ceremony.ClientData.Override = "not json";

            AssertFailure(WebAuthnRegistrationFailureReason.MalformedClientData, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_PublicKeyThatIsNotAWellFormedCoseKey_IsRejected()
        {
            // An EC2 key whose x coordinate is the wrong length: well-formed CBOR, not a usable P-256 key.
            _ceremony.AuthenticatorData.CredentialPublicKey = CborTestEncoder.EncodeCoseEc2Key(new byte[31], new byte[32]);

            AssertFailure(WebAuthnRegistrationFailureReason.MalformedCredentialPublicKey, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_PublicKeyStatingAnAlgorithmOutsideTheSupportedSet_IsRejected()
        {
            // ES384, a real COSE algorithm this library does not verify — so the rejection is the
            // supported-set check and not a decoding failure.
            _ceremony.AuthenticatorData.CredentialPublicKey = CborTestEncoder.EncodeCoseEc2Key(new byte[32], new byte[32], algorithm: -35);

            AssertFailure(WebAuthnRegistrationFailureReason.UnsupportedAlgorithm, _ceremony);
        }

        [TestMethod]
        public void VerifyRegistration_FailedCeremony_CarriesNoCredential()
        {
            _ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;

            WebAuthnRegistrationResult result = Verify(_ceremony);

            Assert.IsNull(result.Credential);
            Assert.IsFalse(result.UserVerified);
            Assert.IsNotNull(result.FailureMessage);
        }

        [TestMethod]
        public void VerifyRegistration_ResponseParsedFromTheBrowsersJson_Succeeds()
        {
            // Every other test here hands the verifier an object built in memory, and the response tests
            // parse hand-written JSON that is deliberately not verifiable. This is the one place the two
            // halves meet: a real ceremony serialized the way a browser serializes it, read back through
            // the public entry point, and verified.
            WebAuthnRegistrationResponse response = WebAuthnRegistrationResponse.FromJson(_ceremony.BuildJson());

            WebAuthnRegistrationResult result = WebAuthnTestData.NewVerifier().VerifyRegistration(
                response,
                WebAuthnTestData.Challenge,
                WebAuthnUserVerificationRequirement.Required);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(Base64UrlEncoding.Encode(_authenticator.CredentialId), result.Credential!.CredentialId);
        }

        private static WebAuthnRegistrationResult Verify(
            SyntheticRegistrationCeremony ceremony,
            WebAuthnUserVerificationRequirement userVerification = WebAuthnUserVerificationRequirement.Required)
        {
            return WebAuthnTestData.NewVerifier().VerifyRegistration(ceremony.Build(), WebAuthnTestData.Challenge, userVerification);
        }

        private static void AssertSucceeds(SyntheticRegistrationCeremony ceremony)
        {
            WebAuthnRegistrationResult result = Verify(ceremony);

            // Reports the reason rather than only that something failed, so a regression here names the
            // check that started rejecting instead of leaving it to be found by bisecting.
            Assert.IsTrue(result.Success, result.FailureMessage);
        }

        private static void AssertFailure(WebAuthnRegistrationFailureReason expectedReason, SyntheticRegistrationCeremony ceremony)
        {
            WebAuthnRegistrationResult result = Verify(ceremony);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(expectedReason, result.FailureReason);
        }
    }
}
