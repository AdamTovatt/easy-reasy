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
        public void ToString_ShouldReturnJson()
        {
            RefreshRequest request = new RefreshRequest("my-refresh-token");

            string result = request.ToString();

            Assert.AreEqual(request.ToJson(), result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void FromJson_WithInvalidJson_ShouldThrowArgumentException()
        {
            RefreshRequest.FromJson("not-valid-json");
        }
    }
}
