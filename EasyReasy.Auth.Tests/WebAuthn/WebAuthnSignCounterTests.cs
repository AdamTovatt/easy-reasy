namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The signature-counter rule, which is not the strict monotonic comparison it looks like it should be.
    /// </summary>
    /// <remarks>
    /// Two things that look identical in the data have to be told apart: a counter that failed to advance
    /// because the credential was cloned, and one that reads 0 forever because the authenticator keeps
    /// none. Getting this wrong in the strict direction rejects every Touch ID and Face ID assertion — the
    /// factor most of this feature's users will have — and getting it wrong in the lax direction silences
    /// the only clone signal WebAuthn offers. Each row below is one of the two.
    /// </remarks>
    [TestClass]
    public class WebAuthnSignCounterTests
    {
        private SyntheticAuthenticator _authenticator = null!;
        private SyntheticAuthenticationCeremony _ceremony = null!;

        /// <summary>
        /// Builds an assertion that verifies. Held as fields so each test below is the one pair of counters
        /// it is about, with nothing between its name and the values it sets.
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
        public void VerifyAuthentication_BothCountersZero_IsAcceptedAsAnAuthenticatorThatKeepsNoCounter()
        {
            // The case that matters most: a platform authenticator reports 0 on every assertion, so a
            // strict "must advance" rule would reject Touch ID and Face ID outright.
            WebAuthnAuthenticationResult result = Verify(storedSignCount: 0, presentedSignCount: 0);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(WebAuthnSignCounterState.NotSupported, result.SignCounterState);
            Assert.AreEqual(0u, result.SignCount);
        }

        [TestMethod]
        public void VerifyAuthentication_CounterAdvanced_IsAcceptedAndReportsTheNewValue()
        {
            WebAuthnAuthenticationResult result = Verify(storedSignCount: 5, presentedSignCount: 6);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(WebAuthnSignCounterState.Advanced, result.SignCounterState);
            Assert.AreEqual(6u, result.SignCount);
        }

        [TestMethod]
        public void VerifyAuthentication_CounterAdvancedByMoreThanOne_IsAccepted()
        {
            // Assertions to other relying parties advance the same counter, so a gap is ordinary; only a
            // failure to advance is a signal.
            WebAuthnAuthenticationResult result = Verify(storedSignCount: 5, presentedSignCount: 500);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(WebAuthnSignCounterState.Advanced, result.SignCounterState);
            Assert.AreEqual(500u, result.SignCount);
        }

        [TestMethod]
        public void VerifyAuthentication_CounterWentBackwards_IsRejectedAsARegression()
        {
            AssertRegression(storedSignCount: 10, presentedSignCount: 9);
        }

        [TestMethod]
        public void VerifyAuthentication_CounterDidNotMove_IsRejectedAsARegression()
        {
            // Equal counts are a regression, not a pass: an authenticator that keeps a counter increments
            // it on every assertion, so repeating a value means two copies of one key.
            AssertRegression(storedSignCount: 10, presentedSignCount: 10);
        }

        [TestMethod]
        public void VerifyAuthentication_StoredCounterNonZeroAndPresentedZero_IsRejectedAsARegression()
        {
            // The authenticator kept a counter at registration and now reports none — a cloned credential
            // answering in place of the real one. Skipping the check whenever the presented value is zero,
            // rather than only when both are, is the mistake this pins.
            AssertRegression(storedSignCount: 7, presentedSignCount: 0);
        }

        [TestMethod]
        public void VerifyAuthentication_StoredCounterZeroAndPresentedNonZero_IsAccepted()
        {
            // The other half of the same boundary: an authenticator whose counter started moving after a
            // registration that recorded 0. Advancing from zero is advancing.
            WebAuthnAuthenticationResult result = Verify(storedSignCount: 0, presentedSignCount: 1);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(WebAuthnSignCounterState.Advanced, result.SignCounterState);
        }

        [TestMethod]
        public void VerifyAuthentication_CounterAtItsMaximum_IsAccepted()
        {
            WebAuthnAuthenticationResult result = Verify(storedSignCount: uint.MaxValue - 1, presentedSignCount: uint.MaxValue);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(WebAuthnSignCounterState.Advanced, result.SignCounterState);
            Assert.AreEqual(uint.MaxValue, result.SignCount);
        }

        private WebAuthnAuthenticationResult Verify(uint storedSignCount, uint presentedSignCount)
        {
            _ceremony.AuthenticatorData.SignCount = presentedSignCount;

            return WebAuthnTestData.NewVerifier().VerifyAuthentication(
                _ceremony.Build(),
                WebAuthnTestData.Challenge,
                _ceremony.StoredCredential(storedSignCount),
                WebAuthnUserVerificationRequirement.Required);
        }

        private void AssertRegression(uint storedSignCount, uint presentedSignCount)
        {
            WebAuthnAuthenticationResult result = Verify(storedSignCount, presentedSignCount);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(WebAuthnAuthenticationFailureReason.SignCounterRegressed, result.FailureReason);

            // The state has to be absent, not NotSupported. The two are the opposite findings — the clone
            // check fired against the clone check never running — and a regression reported as
            // NotSupported would tell an application that this authenticator keeps no counter, which is
            // exactly what the regression proves it does.
            Assert.IsNull(result.SignCounterState);
        }
    }
}
