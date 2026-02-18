namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class RefreshResultTests
    {
        [TestMethod]
        public void Succeeded_ShouldCreateSuccessfulResult()
        {
            AuthResponse authResponse = new AuthResponse("jwt-token", "2025-01-01T00:00:00Z", "new-refresh-token");

            RefreshResult result = RefreshResult.Succeeded(authResponse, "new-refresh-token");

            Assert.IsTrue(result.Success);
            Assert.AreSame(authResponse, result.AuthResponse);
            Assert.AreEqual("new-refresh-token", result.NewRefreshToken);
            Assert.IsNull(result.FailureReason);
        }

        [TestMethod]
        public void Failed_ShouldCreateFailedResult()
        {
            RefreshResult result = RefreshResult.Failed(RefreshFailureReason.TokenExpired);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.AuthResponse);
            Assert.IsNull(result.NewRefreshToken);
            Assert.AreEqual(RefreshFailureReason.TokenExpired, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithTheftDetected_ShouldCreateFailedResult()
        {
            RefreshResult result = RefreshResult.Failed(RefreshFailureReason.TheftDetected);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RefreshFailureReason.TheftDetected, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithTokenNotFound_ShouldCreateFailedResult()
        {
            RefreshResult result = RefreshResult.Failed(RefreshFailureReason.TokenNotFound);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RefreshFailureReason.TokenNotFound, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithTokenInvalidated_ShouldCreateFailedResult()
        {
            RefreshResult result = RefreshResult.Failed(RefreshFailureReason.TokenInvalidated);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RefreshFailureReason.TokenInvalidated, result.FailureReason);
        }
    }
}
