namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The validator refuses inputs it cannot check a response against.
    /// </summary>
    [TestClass]
    public class SamlValidatorArgumentTests
    {
        [TestMethod]
        public void Validate_EmptyExpectedRequestId_Throws()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            string samlResponse = harness.Respond().ToFormValue();

            Assert.ThrowsException<ArgumentException>(() => harness.Validator.Validate(samlResponse, " ", harness.IdentityProvider.Provider));
        }
    }
}
