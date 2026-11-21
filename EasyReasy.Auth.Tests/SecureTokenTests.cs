namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class SecureTokenTests
    {
        [TestMethod]
        public void Hash_SameToken_ReturnsTheSameSha256Hex()
        {
            string token = "test-token-value";

            string hash1 = SecureToken.Hash(token);
            string hash2 = SecureToken.Hash(token);

            Assert.AreEqual(hash1, hash2);
            Assert.AreEqual(64, hash1.Length); // SHA-256 produces 32 bytes = 64 hex chars
        }
    }
}
