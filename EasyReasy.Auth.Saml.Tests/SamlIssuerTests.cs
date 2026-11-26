using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Cryptography;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A response must come from the identity provider the request named: signed with a key pinned for it, and issued
    /// under its entity id in both the response and the assertion.
    /// </summary>
    [TestClass]
    public class SamlIssuerTests
    {
        [TestMethod]
        public void Validate_GenuineResponseFromAnotherIdentityProvider_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.OtherIdentityProvider.Respond(harness.RequestId, harness.ServiceProvider);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "Invalid Issuer");
        }

        [TestMethod]
        public void Validate_AnotherIdentityProvidersKeyUnderExpectedEntityId_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SigningCertificate = SamlTestCertificates.OtherIdentityProvider;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Signature is invalid");
        }

        [TestMethod]
        public void Validate_ResponseIssuerIsAnotherIdentityProvider_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.ResponseIssuer = harness.OtherIdentityProvider.EntityId;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "Invalid Issuer");
        }

        [TestMethod]
        public void Validate_NoResponseIssuer_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.ResponseIssuer = null;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "Invalid Issuer");
        }

        [TestMethod]
        public void Validate_AssertionIssuerIsAnotherIdentityProvider_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.AssertionIssuer = harness.OtherIdentityProvider.EntityId;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "assertion's Issuer");
        }
    }
}
