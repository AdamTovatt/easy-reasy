namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class LogoutRequestTests
    {
        [TestMethod]
        public void ToJson_ShouldSerializeCorrectly()
        {
            LogoutRequest request = new LogoutRequest("my-refresh-token");

            string json = request.ToJson();

            Assert.IsTrue(json.Contains("refreshToken"));
            Assert.IsTrue(json.Contains("my-refresh-token"));
        }

        [TestMethod]
        public void FromJson_ShouldDeserializeCorrectly()
        {
            LogoutRequest original = new LogoutRequest("my-refresh-token");
            string json = original.ToJson();

            LogoutRequest deserialized = LogoutRequest.FromJson(json);

            Assert.AreEqual("my-refresh-token", deserialized.RefreshToken);
        }

        [TestMethod]
        public void ToString_ShouldRedactRefreshToken()
        {
            LogoutRequest request = new LogoutRequest("my-refresh-token");

            string result = request.ToString();

            Assert.IsTrue(result.Contains("[REDACTED]"));
            Assert.IsFalse(result.Contains("my-refresh-token"));
        }

        [TestMethod]
        public void FromJson_WithInvalidJson_ShouldNotLeakInputInException()
        {
            string sensitiveJson = "{\"refreshToken\":\"secret-token-value\",invalid}";

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => LogoutRequest.FromJson(sensitiveJson));

            Assert.IsFalse(exception.Message.Contains("secret-token-value"));
            Assert.IsNull(exception.InnerException);
        }

        [TestMethod]
        public void FromJson_WithInvalidJson_ShouldThrowArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(
                () => LogoutRequest.FromJson("not-valid-json"));
        }
    }
}
