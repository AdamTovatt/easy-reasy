namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class ExternalAuthResultTests
    {
        [TestMethod]
        public void Succeeded_ShouldCreateSuccessfulResult()
        {
            AuthResponse authResponse = new AuthResponse("jwt-token", "2025-01-01T00:00:00Z");

            ExternalAuthResult result = ExternalAuthResult.Succeeded("google", authResponse, "user-42");

            Assert.AreEqual("google", result.Provider);
            Assert.IsTrue(result.Success);
            Assert.AreSame(authResponse, result.AuthResponse);
            Assert.AreEqual("user-42", result.AttemptedSubject);
            Assert.IsNull(result.FailureReason);
        }

        [TestMethod]
        public void Failed_ShouldCreateFailedResult()
        {
            ExternalAuthResult result = ExternalAuthResult.Failed("google", ExternalAuthFailureReason.InvalidToken);

            Assert.AreEqual("google", result.Provider);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.AuthResponse);
            Assert.AreEqual(ExternalAuthFailureReason.InvalidToken, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithoutAttemptedSubject_ShouldDefaultToNull()
        {
            ExternalAuthResult result = ExternalAuthResult.Failed("google", ExternalAuthFailureReason.InvalidToken);

            Assert.IsNull(result.AttemptedSubject);
        }

        [TestMethod]
        public void Failed_WithAttemptedSubject_ShouldPopulateIt()
        {
            ExternalAuthResult result = ExternalAuthResult.Failed("google", ExternalAuthFailureReason.Rejected, attemptedSubject: "sub-99");

            Assert.AreEqual("sub-99", result.AttemptedSubject);
            Assert.AreEqual(ExternalAuthFailureReason.Rejected, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithEachReason_ShouldSetReasonCorrectly()
        {
            Assert.AreEqual(ExternalAuthFailureReason.InvalidToken, ExternalAuthResult.Failed("google", ExternalAuthFailureReason.InvalidToken).FailureReason);
            Assert.AreEqual(ExternalAuthFailureReason.Rejected, ExternalAuthResult.Failed("google", ExternalAuthFailureReason.Rejected).FailureReason);
            Assert.AreEqual(ExternalAuthFailureReason.Other, ExternalAuthResult.Failed("google", ExternalAuthFailureReason.Other).FailureReason);
        }
    }
}
