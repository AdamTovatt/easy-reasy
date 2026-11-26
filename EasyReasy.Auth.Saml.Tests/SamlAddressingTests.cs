using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A response must be meant for this service provider: its audience restrictions name this entity id, and its
    /// destination and bearer recipient name this assertion consumer service URL. A response the identity provider
    /// issued to another service provider is refused here.
    /// </summary>
    [TestClass]
    public class SamlAddressingTests
    {
        private const string OtherServiceProvider = "https://other-app.example.test/saml";
        private const string OtherAssertionConsumerService = "https://other-app.example.test/auth/saml/acs";

        [TestMethod]
        public void Validate_AudienceIsAnotherServiceProvider_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.AudienceRestrictions = [[OtherServiceProvider]];

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<SecurityTokenInvalidAudienceException>(rejection);
        }

        [TestMethod]
        public void Validate_SecondAudienceRestrictionOmitsThisServiceProvider_IsRefused()
        {
            // Each restriction is its own condition; matching one of them is not enough. IdentityModel refuses this
            // before the package's own check does.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.AudienceRestrictions = [[SamlValidationHarness.ServiceProviderEntityId], [OtherServiceProvider]];

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<SecurityTokenInvalidAudienceException>(rejection);
        }

        [TestMethod]
        public void Validate_NoAudienceRestriction_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.AudienceRestrictions = [];

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<Saml2SecurityTokenException>(rejection, "no AudienceRestrictions");
        }

        [TestMethod]
        public void Validate_RecipientIsAnotherAssertionConsumerService_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.Recipient = OtherAssertionConsumerService;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "bearer confirmation's Recipient");
        }

        [TestMethod]
        public void Validate_NoRecipient_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.Recipient = null;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "bearer confirmation's Recipient");
        }

        [TestMethod]
        public void Validate_DestinationIsAnotherAssertionConsumerService_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.Destination = OtherAssertionConsumerService;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "response's Destination");
        }

        [TestMethod]
        public void Validate_NoDestination_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.Destination = null;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "response's Destination");
        }
    }
}
