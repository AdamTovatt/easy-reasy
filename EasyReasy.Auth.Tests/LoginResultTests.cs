namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class LoginResultTests
    {
        [TestMethod]
        public void Succeeded_ShouldCreateSuccessfulResult()
        {
            AuthResponse authResponse = new AuthResponse("jwt-token", "2025-01-01T00:00:00Z");

            LoginResult result = LoginResult.Succeeded(authResponse, "user-42");

            Assert.IsTrue(result.Success);
            Assert.AreSame(authResponse, result.AuthResponse);
            Assert.AreEqual("user-42", result.AttemptedSubject);
            Assert.IsNull(result.FailureReason);
        }

        [TestMethod]
        public void Failed_ShouldCreateFailedResult()
        {
            LoginResult result = LoginResult.Failed(LoginFailureReason.InvalidCredentials);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.AuthResponse);
            Assert.AreEqual(LoginFailureReason.InvalidCredentials, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithoutAttemptedSubject_ShouldDefaultToNull()
        {
            LoginResult result = LoginResult.Failed(LoginFailureReason.UnknownUser);

            Assert.IsNull(result.AttemptedSubject);
        }

        [TestMethod]
        public void Failed_WithAttemptedSubject_ShouldPopulateIt()
        {
            LoginResult result = LoginResult.Failed(LoginFailureReason.UnknownUser, attemptedSubject: "ghost-user");

            Assert.AreEqual("ghost-user", result.AttemptedSubject);
            Assert.AreEqual(LoginFailureReason.UnknownUser, result.FailureReason);
        }

        [TestMethod]
        public void Failed_WithEachReason_ShouldSetReasonCorrectly()
        {
            Assert.AreEqual(LoginFailureReason.UnknownUser, LoginResult.Failed(LoginFailureReason.UnknownUser).FailureReason);
            Assert.AreEqual(LoginFailureReason.InvalidCredentials, LoginResult.Failed(LoginFailureReason.InvalidCredentials).FailureReason);
            Assert.AreEqual(LoginFailureReason.UserDisabled, LoginResult.Failed(LoginFailureReason.UserDisabled).FailureReason);
            Assert.AreEqual(LoginFailureReason.UserLocked, LoginResult.Failed(LoginFailureReason.UserLocked).FailureReason);
            Assert.AreEqual(LoginFailureReason.Other, LoginResult.Failed(LoginFailureReason.Other).FailureReason);
        }
    }
}
