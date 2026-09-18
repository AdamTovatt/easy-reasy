using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The audit hooks a consumer calls after verifying a ceremony. This library ships no endpoint for
    /// either ceremony — half of each runs in the browser — so unlike the other hooks nothing here fires
    /// them; what these tests pin is that they are callable through the interface and that not
    /// implementing them is allowed.
    /// </summary>
    [TestClass]
    public class WebAuthnAuditLoggerTests
    {
        [TestMethod]
        public async Task OnWebAuthnRegistrationAsync_Implemented_ReceivesTheResult()
        {
            RecordingAuditLogger logger = new RecordingAuditLogger();
            DefaultHttpContext httpContext = new DefaultHttpContext();
            WebAuthnRegistrationResult result = VerifyADeclinedRegistration();

            await ((IAuthAuditLogger)logger).OnWebAuthnRegistrationAsync(httpContext, result);

            Assert.AreEqual(1, logger.WebAuthnRegistrationCalls.Count);
            Assert.AreSame(httpContext, logger.WebAuthnRegistrationCalls[0].HttpContext);
            Assert.AreEqual(WebAuthnRegistrationFailureReason.ChallengeMismatch, logger.WebAuthnRegistrationCalls[0].Result.FailureReason);
        }

        [TestMethod]
        public async Task OnWebAuthnRegistrationAsync_NotImplemented_IsANoOp()
        {
            // The whole point of the default: an existing logger keeps compiling and running when another
            // hook is added to the interface.
            IAuthAuditLogger logger = new LoggerImplementingNothing();

            await logger.OnWebAuthnRegistrationAsync(new DefaultHttpContext(), VerifyADeclinedRegistration());
        }

        [TestMethod]
        public async Task OnWebAuthnAuthenticationAsync_Implemented_ReceivesTheResult()
        {
            RecordingAuditLogger logger = new RecordingAuditLogger();
            DefaultHttpContext httpContext = new DefaultHttpContext();
            WebAuthnAuthenticationResult result = VerifyADeclinedAssertion();

            await ((IAuthAuditLogger)logger).OnWebAuthnAuthenticationAsync(httpContext, result);

            Assert.AreEqual(1, logger.WebAuthnAuthenticationCalls.Count);
            Assert.AreSame(httpContext, logger.WebAuthnAuthenticationCalls[0].HttpContext);
            Assert.AreEqual(WebAuthnAuthenticationFailureReason.SignCounterRegressed, logger.WebAuthnAuthenticationCalls[0].Result.FailureReason);
        }

        [TestMethod]
        public async Task OnWebAuthnAuthenticationAsync_NotImplemented_IsANoOp()
        {
            IAuthAuditLogger logger = new LoggerImplementingNothing();

            await logger.OnWebAuthnAuthenticationAsync(new DefaultHttpContext(), VerifyADeclinedAssertion());
        }

        [TestMethod]
        public async Task OnWebAuthnAuthenticationAsync_RegistrationLoggedFirst_KeepsTheTwoCeremoniesApart()
        {
            // Two hooks taking a differently-typed result each, so a logger recording one under the other
            // would not compile — but a hook wired to the wrong list would, and this is what catches it.
            RecordingAuditLogger logger = new RecordingAuditLogger();

            await ((IAuthAuditLogger)logger).OnWebAuthnRegistrationAsync(new DefaultHttpContext(), VerifyADeclinedRegistration());
            await ((IAuthAuditLogger)logger).OnWebAuthnAuthenticationAsync(new DefaultHttpContext(), VerifyADeclinedAssertion());

            Assert.AreEqual(1, logger.WebAuthnRegistrationCalls.Count);
            Assert.AreEqual(1, logger.WebAuthnAuthenticationCalls.Count);
        }

        /// <summary>
        /// Runs a real ceremony that is declined, so the recorded result carries the reason an audit log is
        /// meant to keep rather than a value assembled for the assertion.
        /// </summary>
        private static WebAuthnRegistrationResult VerifyADeclinedRegistration()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            SyntheticRegistrationCeremony ceremony = new SyntheticRegistrationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.ClientData.Challenge = WebAuthnTestData.OtherChallenge;

            return WebAuthnTestData.NewVerifier().VerifyRegistration(
                ceremony.Build(),
                WebAuthnTestData.Challenge,
                WebAuthnUserVerificationRequirement.Required);
        }

        /// <summary>
        /// Runs a real assertion declined for the reason most worth alerting on — the counter regression
        /// WebAuthn names as its one signal of a cloned credential.
        /// </summary>
        private static WebAuthnAuthenticationResult VerifyADeclinedAssertion()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            SyntheticAuthenticationCeremony ceremony = new SyntheticAuthenticationCeremony(authenticator, WebAuthnTestData.Challenge);
            ceremony.AuthenticatorData.SignCount = 3;

            return WebAuthnTestData.NewVerifier().VerifyAuthentication(
                ceremony.Build(),
                WebAuthnTestData.Challenge,
                ceremony.StoredCredential(signCount: 9),
                WebAuthnUserVerificationRequirement.Required);
        }

        private sealed class LoggerImplementingNothing : IAuthAuditLogger
        {
        }
    }
}
