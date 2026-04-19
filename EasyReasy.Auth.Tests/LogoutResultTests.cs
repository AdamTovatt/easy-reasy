namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class LogoutResultTests
    {
        [TestMethod]
        public void Known_ShouldPopulateSubjectAndFamilyId()
        {
            LogoutResult result = LogoutResult.Known("user-42", "family-abc");

            Assert.IsTrue(result.WasKnown);
            Assert.AreEqual("user-42", result.Subject);
            Assert.AreEqual("family-abc", result.FamilyId);
        }

        [TestMethod]
        public void Unknown_ShouldHaveNullSubjectAndFamilyId()
        {
            LogoutResult result = LogoutResult.Unknown();

            Assert.IsFalse(result.WasKnown);
            Assert.IsNull(result.Subject);
            Assert.IsNull(result.FamilyId);
        }
    }
}
