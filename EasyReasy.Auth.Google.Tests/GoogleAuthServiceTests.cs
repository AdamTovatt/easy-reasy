using EasyReasy.Auth.Google;
using System.Security.Claims;

namespace EasyReasy.Auth.Google.Tests
{
    [TestClass]
    public class GoogleAuthServiceTests
    {
        private FakeGoogleIdTokenValidator _fakeValidator = null!;
        private FakeGoogleAuthHandler _fakeHandler = null!;
        private StubJwtTokenService _stubJwtTokenService = null!;
        private GoogleAuthService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _fakeValidator = new FakeGoogleIdTokenValidator();
            _fakeHandler = new FakeGoogleAuthHandler();
            _stubJwtTokenService = new StubJwtTokenService();
            _service = new GoogleAuthService(_fakeValidator, _fakeHandler);
        }

        [TestMethod]
        public async Task AuthenticateAsync_ValidToken_ReturnsSucceededResult()
        {
            GoogleUserInfo userInfo = new GoogleUserInfo
            {
                Subject = "sub-123",
                Email = "test@example.com",
                EmailVerified = true,
                Name = "Test User",
            };
            _fakeValidator.UserInfoToReturn = userInfo;

            AuthResponse expectedResponse = new AuthResponse("jwt-token", "2099-01-01T00:00:00Z");
            _fakeHandler.ResponseToReturn = expectedResponse;

            ExternalAuthResult result = await _service.AuthenticateAsync("valid-google-token", _stubJwtTokenService, null, null);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("google", result.Provider);
            Assert.AreEqual("sub-123", result.AttemptedSubject);
            Assert.IsNull(result.FailureReason);
            Assert.IsNotNull(result.AuthResponse);
            Assert.AreEqual("jwt-token", result.AuthResponse.Token);
            Assert.AreEqual("2099-01-01T00:00:00Z", result.AuthResponse.ExpiresAt);
        }

        [TestMethod]
        public async Task AuthenticateAsync_InvalidToken_ReturnsInvalidTokenFailure()
        {
            _fakeValidator.ExceptionToThrow = new GoogleTokenValidationException("Invalid token");

            ExternalAuthResult result = await _service.AuthenticateAsync("bad-token", _stubJwtTokenService, null, null);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("google", result.Provider);
            Assert.IsNull(result.AuthResponse);
            Assert.AreEqual(ExternalAuthFailureReason.InvalidToken, result.FailureReason);
            Assert.IsNull(result.AttemptedSubject);
        }

        [TestMethod]
        public async Task AuthenticateAsync_InvalidToken_HandlerNotCalled()
        {
            _fakeValidator.ExceptionToThrow = new GoogleTokenValidationException("Invalid token");

            await _service.AuthenticateAsync("bad-token", _stubJwtTokenService, null, null);

            Assert.IsFalse(_fakeHandler.WasCalled);
        }

        [TestMethod]
        public async Task AuthenticateAsync_HandlerReturnsNull_ReturnsRejectedFailure()
        {
            _fakeValidator.UserInfoToReturn = new GoogleUserInfo
            {
                Subject = "sub-456",
                Email = "rejected@example.com",
                EmailVerified = true,
            };
            _fakeHandler.ResponseToReturn = null;

            ExternalAuthResult result = await _service.AuthenticateAsync("valid-token", _stubJwtTokenService, null, null);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("google", result.Provider);
            Assert.IsNull(result.AuthResponse);
            Assert.AreEqual(ExternalAuthFailureReason.Rejected, result.FailureReason);
            Assert.AreEqual("sub-456", result.AttemptedSubject);
        }

        [TestMethod]
        public async Task AuthenticateAsync_UserInfoPassedToHandler()
        {
            GoogleUserInfo userInfo = new GoogleUserInfo
            {
                Subject = "sub-789",
                Email = "details@example.com",
                EmailVerified = true,
                Name = "Detailed User",
                PictureUrl = "https://example.com/pic.jpg",
            };
            _fakeValidator.UserInfoToReturn = userInfo;
            _fakeHandler.ResponseToReturn = new AuthResponse("token", "2099-01-01T00:00:00Z");

            await _service.AuthenticateAsync("some-token", _stubJwtTokenService, null, null);

            Assert.IsNotNull(_fakeHandler.LastReceivedUserInfo);
            Assert.AreEqual("sub-789", _fakeHandler.LastReceivedUserInfo.Subject);
            Assert.AreEqual("details@example.com", _fakeHandler.LastReceivedUserInfo.Email);
            Assert.AreEqual("Detailed User", _fakeHandler.LastReceivedUserInfo.Name);
            Assert.AreEqual("https://example.com/pic.jpg", _fakeHandler.LastReceivedUserInfo.PictureUrl);
        }

        [TestMethod]
        public async Task AuthenticateAsync_IdTokenPassedToValidator()
        {
            _fakeValidator.UserInfoToReturn = new GoogleUserInfo
            {
                Subject = "sub-abc",
                Email = "abc@example.com",
                EmailVerified = true,
            };
            _fakeHandler.ResponseToReturn = new AuthResponse("token", "2099-01-01T00:00:00Z");

            await _service.AuthenticateAsync("specific-id-token", _stubJwtTokenService, null, null);

            Assert.AreEqual("specific-id-token", _fakeValidator.LastValidatedToken);
        }

        /// <summary>
        /// Minimal stub for <see cref="IJwtTokenService"/> used in tests where the service
        /// is passed through but not directly invoked by <see cref="GoogleAuthService"/>.
        /// </summary>
        private class StubJwtTokenService : IJwtTokenService
        {
            public string CreateToken(
                string subject,
                string authType,
                IEnumerable<Claim> additionalClaims,
                IEnumerable<string> roles,
                DateTime expiresAt)
            {
                return "stub-token";
            }
        }
    }
}
