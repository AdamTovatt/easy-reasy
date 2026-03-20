namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class RefreshRequestTests
    {
        [TestMethod]
        public void ToJson_ShouldSerializeCorrectly()
        {
            RefreshRequest request = new RefreshRequest("my-refresh-token");

            string json = request.ToJson();

            Assert.IsTrue(json.Contains("refreshToken"));
            Assert.IsTrue(json.Contains("my-refresh-token"));
        }

        [TestMethod]
        public void FromJson_ShouldDeserializeCorrectly()
        {
            RefreshRequest original = new RefreshRequest("my-refresh-token");
            string json = original.ToJson();

            RefreshRequest deserialized = RefreshRequest.FromJson(json);

            Assert.AreEqual("my-refresh-token", deserialized.RefreshToken);
        }

        [TestMethod]
        public void ToString_ShouldRedactRefreshToken()
        {
            RefreshRequest request = new RefreshRequest("my-refresh-token");

            string result = request.ToString();

            Assert.IsTrue(result.Contains("[REDACTED]"));
            Assert.IsFalse(result.Contains("my-refresh-token"));
        }

        [TestMethod]
        public void FromJson_WithInvalidJson_ShouldNotLeakInputInException()
        {
            string sensitiveJson = "{\"refreshToken\":\"secret-token-value\",invalid}";

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => RefreshRequest.FromJson(sensitiveJson));

            Assert.IsFalse(exception.Message.Contains("secret-token-value"));
            Assert.IsNull(exception.InnerException);
        }

        [TestMethod]
        public void FromJson_WithInvalidJson_ShouldThrowArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(
                () => RefreshRequest.FromJson("not-valid-json"));
        }
    }
}
