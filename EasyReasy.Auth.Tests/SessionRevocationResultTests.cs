namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class SessionRevocationResultTests
    {
        [TestMethod]
        public void Constructor_ShouldPopulateProperties()
        {
            SessionRevocationResult result = new SessionRevocationResult("user-42", 3);

            Assert.AreEqual("user-42", result.Subject);
            Assert.AreEqual(3, result.InvalidatedFamilyCount);
        }

        [TestMethod]
        public void Constructor_WithZeroCount_ShouldAllowZero()
        {
            SessionRevocationResult result = new SessionRevocationResult("user-42", 0);

            Assert.AreEqual(0, result.InvalidatedFamilyCount);
        }
    }
}
