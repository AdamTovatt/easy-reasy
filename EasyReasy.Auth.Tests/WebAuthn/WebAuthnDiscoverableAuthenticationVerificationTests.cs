using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// <see cref="WebAuthnVerifier.VerifyDiscoverableAuthentication"/>: a usernameless passkey sign-in,
    /// verified against a real authenticator that returns its user handle. Each test takes an assertion
    /// that verifies and breaks the one thing its check reads. What the method refuses to be called with
    /// is in <see cref="WebAuthnVerifierArgumentTests"/>.
    /// </summary>
    [TestClass]
    public class WebAuthnDiscoverableAuthenticationVerificationTests
    {
        private SyntheticAuthenticator _authenticator = null!;
        private SyntheticAuthenticationCeremony _ceremony = null!;

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

        [DataTestMethod]
        [DataRow(CoseAlgorithm.Es256)]
        [DataRow(CoseAlgorithm.Rs256)]
        public void VerifyDiscoverableAuthentication_CorrectCeremony_SucceedsAndReportsTheCounterInDiscoverableMode(CoseAlgorithm algorithm)
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create(algorithm);
            SyntheticAuthenticationCeremony ceremony = new SyntheticAuthenticationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.AuthenticatorData.SignCount = 8;
            ceremony.AuthenticatorData.BackupEligible = true;
            ceremony.AuthenticatorData.BackedUp = true;

            WebAuthnAuthenticationResult result = WebAuthnTestData.NewVerifier().VerifyDiscoverableAuthentication(
                ceremony.Build(),
                WebAuthnTestData.Challenge,
                ceremony.StoredCredential(signCount: 5),
                ceremony.OwnerUserHandle);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.IsNull(result.FailureReason);
            Assert.AreEqual(WebAuthnAuthenticationMode.Discoverable, result.Mode);
            Assert.AreEqual(8u, result.SignCount);
            Assert.AreEqual(WebAuthnSignCounterState.Advanced, result.SignCounterState);
            Assert.IsTrue(result.UserVerified);
            Assert.IsTrue(result.BackedUp);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_ResponseParsedFromTheBrowsersJson_Succeeds()
        {
            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(_ceremony.BuildJson());

            WebAuthnAuthenticationResult result = WebAuthnTestData.NewVerifier().VerifyDiscoverableAuthentication(
                response,
                WebAuthnTestData.Challenge,
                _ceremony.StoredCredential(),
                _ceremony.OwnerUserHandle);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(WebAuthnSignCounterState.NotSupported, result.SignCounterState);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_NoUserHandle_IsRejected()
        {
            _ceremony.UserHandle = null;

            AssertFailure(WebAuthnAuthenticationFailureReason.UserHandleMissing);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_AnotherUsersHandle_IsRejected()
        {
            // Same length as the owner's and differing in every byte, so neither a length check nor a
            // comparison of a prefix can pass it.
            byte[] otherUserHandle = _authenticator.UserHandle.Select(value => (byte)~value).ToArray();
            _ceremony.UserHandle = Base64UrlEncoding.Encode(otherUserHandle);

            AssertFailure(WebAuthnAuthenticationFailureReason.UserHandleMismatch);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_UserHandleThatIsAPrefixOfTheOwners_IsRejected()
        {
            _ceremony.UserHandle = Base64UrlEncoding.Encode(_authenticator.UserHandle.AsSpan(0, 8));

            AssertFailure(WebAuthnAuthenticationFailureReason.UserHandleMismatch);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_PaddedUserHandleFromTheBrowser_IsAccepted()
        {
            // The browser's spelling is decoded leniently, like every other field it sends.
            _ceremony.UserHandle = Base64UrlEncoding.Encode(_authenticator.UserHandle) + "==";

            Assert.IsTrue(Verify().Success);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_UserVerificationNotReported_IsRejected()
        {
            _ceremony.AuthenticatorData.UserVerified = false;

            AssertFailure(WebAuthnAuthenticationFailureReason.UserNotVerified);
        }

        [DataTestMethod]
        [DataRow("ceremonyType", WebAuthnAuthenticationFailureReason.WrongCeremonyType)]
        [DataRow("challenge", WebAuthnAuthenticationFailureReason.ChallengeMismatch)]
        [DataRow("origin", WebAuthnAuthenticationFailureReason.OriginNotAllowed)]
        [DataRow("crossOrigin", WebAuthnAuthenticationFailureReason.CrossOriginCeremony)]
        [DataRow("relyingPartyIdHash", WebAuthnAuthenticationFailureReason.RelyingPartyIdHashMismatch)]
        [DataRow("userPresence", WebAuthnAuthenticationFailureReason.UserNotPresent)]
        [DataRow("signature", WebAuthnAuthenticationFailureReason.SignatureInvalid)]
        public void VerifyDiscoverableAuthentication_EachSecondFactorCheckBroken_IsRejectedForThatCheck(
            string brokenField,
            WebAuthnAuthenticationFailureReason expectedReason)
        {
            // The checks the second factor makes run here too. The challenge row is the replay case: in this
            // mode nothing else ties an assertion to the ceremony that issued it.
            Break(brokenField);

            AssertFailure(expectedReason);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_SignCounterThatRegressed_IsRejected()
        {
            _ceremony.AuthenticatorData.SignCount = 3;

            WebAuthnAuthenticationResult result = Verify(_ceremony.StoredCredential(signCount: 9));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(WebAuthnAuthenticationFailureReason.SignCounterRegressed, result.FailureReason);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_AssertionForAnotherCredential_IsRejectedBeforeTheUserHandleIsRead()
        {
            // The credential id is checked first: an assertion naming another credential says nothing about
            // whose handle it should carry.
            _ceremony.ReportedCredentialId = Base64UrlEncoding.Encode(new byte[] { 0xAA, 0xBB, 0xCC });
            _ceremony.UserHandle = null;

            AssertFailure(WebAuthnAuthenticationFailureReason.CredentialIdMismatch);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void VerifyDiscoverableAuthentication_ForgedSignatureWithABadUserHandle_IsReportedAsTheSignature(bool userHandleMissing)
        {
            // The user handle is not signed, so anyone who knows a credential id can send any handle. If it
            // were checked first, a forger would choose the reason and hide the SignatureInvalid an audit
            // record alerts on.
            _ceremony.SignedBytesOverride = Encoding.UTF8.GetBytes("some other bytes entirely");
            _ceremony.UserHandle = userHandleMissing ? null : Base64UrlEncoding.Encode(new byte[] { 0x0B, 0xAD });

            AssertFailure(WebAuthnAuthenticationFailureReason.SignatureInvalid);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_RegressedCounterWithAnotherUsersHandle_IsReportedAsTheCounter()
        {
            // The counter is signed, so the clone signal also outranks the unsigned handle.
            _ceremony.AuthenticatorData.SignCount = 3;
            _ceremony.UserHandle = Base64UrlEncoding.Encode(new byte[] { 0x0B, 0xAD });

            WebAuthnAuthenticationResult result = Verify(_ceremony.StoredCredential(signCount: 9));

            Assert.AreEqual(WebAuthnAuthenticationFailureReason.SignCounterRegressed, result.FailureReason);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_FailedCeremony_ReportsDiscoverableMode()
        {
            _ceremony.UserHandle = null;

            WebAuthnAuthenticationResult result = Verify();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(WebAuthnAuthenticationMode.Discoverable, result.Mode);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_FailedSharedCheck_ReportsDiscoverableMode()
        {
            // A failure from the checks both modes share, rather than from the user-handle check that only
            // this mode runs.
            _ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;

            WebAuthnAuthenticationResult result = Verify();

            Assert.AreEqual(WebAuthnAuthenticationFailureReason.ChallengeMismatch, result.FailureReason);
            Assert.AreEqual(WebAuthnAuthenticationMode.Discoverable, result.Mode);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_MalformedStructure_ReportsDiscoverableMode()
        {
            // The parse-failure path builds its result apart from the checks, so it is pinned on its own.
            _ceremony.ClientData.Override = "not json";

            WebAuthnAuthenticationResult result = Verify();

            Assert.AreEqual(WebAuthnAuthenticationFailureReason.MalformedClientData, result.FailureReason);
            Assert.AreEqual(WebAuthnAuthenticationMode.Discoverable, result.Mode);
        }

        [TestMethod]
        public void VerifyDiscoverableAuthentication_UserHandleExactlyAtTheLimit_IsAccepted()
        {
            // The upper bound is the one the user entity applies, so a handle it accepted is accepted here.
            byte[] longestHandle = Enumerable.Range(0, PublicKeyCredentialUserEntity.MaximumIdLength).Select(index => (byte)index).ToArray();
            _ceremony.UserHandle = Base64UrlEncoding.Encode(longestHandle);

            WebAuthnAuthenticationResult result = WebAuthnTestData.NewVerifier().VerifyDiscoverableAuthentication(
                _ceremony.Build(),
                WebAuthnTestData.Challenge,
                _ceremony.StoredCredential(),
                longestHandle);

            Assert.IsTrue(result.Success, result.FailureMessage);
        }

        private void Break(string field)
        {
            switch (field)
            {
                case "ceremonyType":
                    _ceremony.ClientData.CeremonyType = CollectedClientData.RegistrationCeremonyType;
                    break;
                case "challenge":
                    _ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;
                    break;
                case "origin":
                    _ceremony.ClientData.Origin = "https://evil.example.com";
                    break;
                case "crossOrigin":
                    _ceremony.ClientData.CrossOrigin = true;
                    break;
                case "relyingPartyIdHash":
                    _ceremony.AuthenticatorData.RelyingPartyIdHash = SHA256.HashData(Encoding.UTF8.GetBytes("other.example.com"));
                    break;
                case "userPresence":
                    _ceremony.AuthenticatorData.UserPresent = false;
                    break;
                case "signature":
                    _ceremony.SignedBytesOverride = Encoding.UTF8.GetBytes("some other bytes entirely");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field), field, "No such field to break.");
            }
        }

        private WebAuthnAuthenticationResult Verify(WebAuthnStoredCredential? storedCredential = null)
        {
            return WebAuthnTestData.NewVerifier().VerifyDiscoverableAuthentication(
                _ceremony.Build(),
                WebAuthnTestData.Challenge,
                storedCredential ?? _ceremony.StoredCredential(),
                _ceremony.OwnerUserHandle);
        }

        private void AssertFailure(WebAuthnAuthenticationFailureReason expectedReason)
        {
            WebAuthnAuthenticationResult result = Verify();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(expectedReason, result.FailureReason);
        }
    }
}
