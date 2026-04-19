namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class ApiKeyAuthResultTests
    {
        [TestMethod]
        public void Succeeded_ShouldCreateSuccessfulResult()
        {
            AuthResponse authResponse = new AuthResponse("jwt-token", "2025-01-01T00:00:00Z");

            ApiKeyAuthResult result = ApiKeyAuthResult.Succeeded(authResponse, "client-42");

            Assert.IsTrue(result.Success);
            Assert.AreSame(authResponse, result.AuthResponse);
            Assert.AreEqual("client-42", result.AttemptedClientId);
            Assert.IsNull(result.FailureReason);
        }

        [TestMethod]
        public void Failed_ShouldCreateFailedResult()
        {
            ApiKeyAuthResult result = ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.AuthResponse);
            Assert.AreEqual(ApiKeyAuthFailureReason.UnknownKey, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithoutAttemptedClientId_ShouldDefaultToNull()
        {
            ApiKeyAuthResult result = ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey);

            Assert.IsNull(result.AttemptedClientId);
        }

        [TestMethod]
        public void Failed_WithAttemptedClientId_ShouldPopulateIt()
        {
            ApiKeyAuthResult result = ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.KeyRevoked, attemptedClientId: "client-x");

            Assert.AreEqual("client-x", result.AttemptedClientId);
            Assert.AreEqual(ApiKeyAuthFailureReason.KeyRevoked, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithEachReason_ShouldSetReasonCorrectly()
        {
            Assert.AreEqual(ApiKeyAuthFailureReason.UnknownKey, ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.UnknownKey).FailureReason);
            Assert.AreEqual(ApiKeyAuthFailureReason.KeyRevoked, ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.KeyRevoked).FailureReason);
            Assert.AreEqual(ApiKeyAuthFailureReason.KeyExpired, ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.KeyExpired).FailureReason);
            Assert.AreEqual(ApiKeyAuthFailureReason.Other, ApiKeyAuthResult.Failed(ApiKeyAuthFailureReason.Other).FailureReason);
        }
    }
}
