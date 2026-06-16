using EasyReasy.Auth.Google;

namespace EasyReasy.Auth.Google.Tests
{
    [TestClass]
    public class GoogleAuthRequestTests
    {
        [TestMethod]
        public void Constructor_SetsIdToken()
        {
            GoogleAuthRequest request = new GoogleAuthRequest("test-token-123");

            Assert.AreEqual("test-token-123", request.IdToken);
        }

        [TestMethod]
        public void ToJson_SerializesCorrectly()
        {
            GoogleAuthRequest request = new GoogleAuthRequest("my-id-token");

            string json = request.ToJson();

            Assert.AreEqual("{\"idToken\":\"my-id-token\"}", json);
        }

        [TestMethod]
        public void FromJson_DeserializesCorrectly()
        {
            string json = "{\"idToken\":\"my-id-token\"}";

            GoogleAuthRequest request = GoogleAuthRequest.FromJson(json);

            Assert.AreEqual("my-id-token", request.IdToken);
        }

        [TestMethod]
        public void FromJson_RoundTrip_PreservesValues()
        {
            GoogleAuthRequest original = new GoogleAuthRequest("round-trip-token");

            string json = original.ToJson();
            GoogleAuthRequest deserialized = GoogleAuthRequest.FromJson(json);

            Assert.AreEqual(original.IdToken, deserialized.IdToken);
        }

        [TestMethod]
        public void ToString_RedactsIdToken()
        {
            GoogleAuthRequest request = new GoogleAuthRequest("super-secret-token");

            string result = request.ToString();

            Assert.AreEqual("{\"idToken\":\"[REDACTED]\"}", result);
            Assert.IsFalse(result.Contains("super-secret-token"));
        }

        [TestMethod]
        public void FromJson_InvalidJson_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => GoogleAuthRequest.FromJson("not valid json"));
        }

        [TestMethod]
        public void FromJson_NullResult_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => GoogleAuthRequest.FromJson("null"));
        }
    }
}
