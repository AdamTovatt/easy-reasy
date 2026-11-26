using ITfoxtec.Identity.Saml2.Cryptography;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The assertion itself must carry a valid signature from a key pinned for the identity provider the request named.
    /// </summary>
    [TestClass]
    public class SamlSignatureRequirementTests
    {
        [TestMethod]
        public void Validate_NothingSigned_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SignAssertion = false;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Signature is invalid");
        }

        [TestMethod]
        public void Validate_OnlyResponseSigned_IsRefused()
        {
            // The library accepts a signed response in place of a signed assertion; the package requires the assertion's own.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SignAssertion = false;
            response.SignResponse = true;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "The assertion is not signed");
        }

        [TestMethod]
        public void Validate_NameIdChangedAfterSigning_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            XmlDocument document = harness.Respond("attacker-1").BuildDocument();
            SamlXmlEdit.NameId(SamlXmlEdit.Assertion(document)).InnerText = "victim-1";

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Signature is invalid");
        }

        [TestMethod]
        public void Validate_SignedByUnpinnedKeyWhoseCertificateIsEmbedded_IsRefused()
        {
            // The signature carries the attacker's certificate in KeyInfo; only the pinned certificates may verify it.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SigningCertificate = SamlTestCertificates.Attacker;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Signature is invalid");
        }

        [TestMethod]
        public void Validate_SignedWithRolloverKeyThatIsNotPinned_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SigningCertificate = SamlTestCertificates.IdentityProviderRollover;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Signature is invalid");
        }

        [TestMethod]
        public void Validate_SecondSignatureInAssertion_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            XmlDocument document = harness.Respond().BuildDocument();
            XmlElement assertion = SamlXmlEdit.Assertion(document);
            XmlElement signature = SamlXmlEdit.Signature(assertion);
            assertion.InsertAfter(signature.CloneNode(true), signature);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "more then one Signature");
        }
    }
}
