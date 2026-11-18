namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// The sign counter: 0 keeps no counter, and once set above 0 every assertion raises it by one.
    /// </summary>
    [TestClass]
    public class WebAuthnTestAuthenticatorSignCountTests
    {
        [TestMethod]
        public void Assert_CounterLeftAtZero_ReportsNoCounterEveryTime()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            WebAuthnStoredCredential stored = TestRelyingParty.Stored(TestRelyingParty.RegisterSecondFactor(authenticator));

            WebAuthnAuthenticationResult first = TestRelyingParty.SignInSecondFactor(authenticator, stored);
            WebAuthnAuthenticationResult second = TestRelyingParty.SignInSecondFactor(authenticator, stored);

            Assert.AreEqual(0u, authenticator.SignCount);
            Assert.AreEqual(0u, second.SignCount);
            Assert.AreEqual(WebAuthnSignCounterState.NotSupported, first.SignCounterState);
            Assert.AreEqual(WebAuthnSignCounterState.NotSupported, second.SignCounterState);
        }

        [TestMethod]
        public void Register_CounterSetAboveZero_ReportsItWithoutAdvancing()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.SignCount = 5;

            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            Assert.AreEqual(5u, credential.SignCount);
            Assert.AreEqual(5u, authenticator.SignCount);
        }

        [TestMethod]
        public void Assert_CounterSetAboveZero_AdvancesByOneEachAssertion()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.SignCount = 5;
            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            WebAuthnAuthenticationResult first = TestRelyingParty.SignInSecondFactor(authenticator, TestRelyingParty.Stored(credential));
            WebAuthnStoredCredential afterFirst = new WebAuthnStoredCredential(credential.CredentialId, credential.PublicKey, first.SignCount);
            WebAuthnAuthenticationResult second = TestRelyingParty.SignInSecondFactor(authenticator, afterFirst);

            Assert.AreEqual(6u, first.SignCount);
            Assert.AreEqual(7u, second.SignCount);
            Assert.AreEqual(7u, authenticator.SignCount);
            Assert.AreEqual(WebAuthnSignCounterState.Advanced, second.SignCounterState);
        }
    }
}
