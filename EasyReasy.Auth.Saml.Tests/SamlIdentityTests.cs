namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// <see cref="SamlIdentity"/> holds no subject longer than an application's account link column is sized for.
    /// </summary>
    [TestClass]
    public class SamlIdentityTests
    {
        [TestMethod]
        public void Constructor_SubjectOfTheMostLength_IsKept()
        {
            string subject = new string('s', SamlIdentity.MaxSubjectLength);

            Assert.AreEqual(subject, new SamlIdentity("acme", "https://idp.example.test/metadata", subject).Subject);
        }

        [TestMethod]
        public void Constructor_SubjectLongerThanTheMost_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new SamlIdentity("acme", "https://idp.example.test/metadata", new string('s', SamlIdentity.MaxSubjectLength + 1)));

            Assert.AreEqual("subject", exception.ParamName);
        }
    }
}
