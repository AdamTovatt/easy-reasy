using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The audit hook a consumer calls after verifying a registration. This library ships no endpoint for
    /// the ceremony — half of it runs in the browser — so unlike the other hooks nothing here fires it;
    /// what these tests pin is that it is callable through the interface and that not implementing it is
    /// allowed.
    /// </summary>
    [TestClass]
    public class WebAuthnAuditLoggerTests
    {
        [TestMethod]
        public async Task OnWebAuthnRegistrationAsync_Implemented_ReceivesTheResult()
        {
            RecordingAuditLogger logger = new RecordingAuditLogger();
            DefaultHttpContext httpContext = new DefaultHttpContext();
            WebAuthnRegistrationResult result = VerifyADeclinedCeremony();

            await ((IAuthAuditLogger)logger).OnWebAuthnRegistrationAsync(httpContext, result);

            Assert.AreEqual(1, logger.WebAuthnRegistrationCalls.Count);
            Assert.AreSame(httpContext, logger.WebAuthnRegistrationCalls[0].HttpContext);
            Assert.AreEqual(WebAuthnRegistrationFailureReason.ChallengeMismatch, logger.WebAuthnRegistrationCalls[0].Result.FailureReason);
        }

        [TestMethod]
        public async Task OnWebAuthnRegistrationAsync_NotImplemented_IsANoOp()
        {
            // The whole point of the default: an existing logger keeps compiling and running when a ninth
            // hook is added to the interface.
            IAuthAuditLogger logger = new LoggerImplementingNothing();

            await logger.OnWebAuthnRegistrationAsync(new DefaultHttpContext(), VerifyADeclinedCeremony());
        }

        /// <summary>
        /// Runs a real ceremony that is declined, so the recorded result carries the reason an audit log is
        /// meant to keep rather than a value assembled for the assertion.
        /// </summary>
        private static WebAuthnRegistrationResult VerifyADeclinedCeremony()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            SyntheticRegistrationCeremony ceremony = new SyntheticRegistrationCeremony(authenticator, WebAuthnTestData.Challenge)
            {
                Challenge = WebAuthnTestData.OtherChallenge,
            };

            return WebAuthnTestData.NewVerifier().VerifyRegistration(
                ceremony.Build(),
                WebAuthnTestData.Challenge,
                WebAuthnUserVerificationRequirement.Required);
        }

        private sealed class LoggerImplementingNothing : IAuthAuditLogger
        {
        }
    }
}
