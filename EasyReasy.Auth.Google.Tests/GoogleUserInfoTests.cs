using EasyReasy.Auth.Google;

namespace EasyReasy.Auth.Google.Tests
{
    [TestClass]
    public class GoogleUserInfoTests
    {
        [TestMethod]
        public void RequiredProperties_AreStored()
        {
            GoogleUserInfo userInfo = new GoogleUserInfo
            {
                Subject = "google-sub-123",
                Email = "user@example.com",
                EmailVerified = true,
            };

            Assert.AreEqual("google-sub-123", userInfo.Subject);
            Assert.AreEqual("user@example.com", userInfo.Email);
            Assert.IsTrue(userInfo.EmailVerified);
        }

        [TestMethod]
        public void OptionalProperties_DefaultToNull()
        {
            GoogleUserInfo userInfo = new GoogleUserInfo
            {
                Subject = "google-sub-456",
                Email = "another@example.com",
                EmailVerified = false,
            };

            Assert.IsNull(userInfo.Name);
            Assert.IsNull(userInfo.PictureUrl);
        }

        [TestMethod]
        public void AllProperties_CanBeSet()
        {
            GoogleUserInfo userInfo = new GoogleUserInfo
            {
                Subject = "google-sub-789",
                Email = "full@example.com",
                EmailVerified = true,
                Name = "Full User",
                PictureUrl = "https://example.com/photo.jpg",
            };

            Assert.AreEqual("google-sub-789", userInfo.Subject);
            Assert.AreEqual("full@example.com", userInfo.Email);
            Assert.IsTrue(userInfo.EmailVerified);
            Assert.AreEqual("Full User", userInfo.Name);
            Assert.AreEqual("https://example.com/photo.jpg", userInfo.PictureUrl);
        }
    }
}
