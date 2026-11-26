namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The settings and the validator refuse inputs they cannot check a response against.
    /// </summary>
    [TestClass]
    public class SamlValidatorArgumentTests
    {
        [TestMethod]
        public void SamlIdentityProviderTrust_NoSigningCertificates_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => new SamlIdentityProviderTrust("https://idp.example.test/metadata", []));
        }

        [TestMethod]
        public void SamlServiceProviderSettings_RelativeAssertionConsumerServiceUrl_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => new SamlServiceProviderSettings(SamlValidationHarness.ServiceProviderEntityId, new Uri("/auth/saml/acs", UriKind.Relative), []));
        }

        [TestMethod]
        public void Validate_EmptyExpectedRequestId_Throws()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            string samlResponse = harness.Respond().ToFormValue();

            Assert.ThrowsException<ArgumentException>(() => harness.Validator.Validate(samlResponse, " ", harness.IdentityProvider.Trust));
        }
    }
}
