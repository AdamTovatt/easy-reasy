using ITfoxtec.Identity.Saml2;
using Microsoft.IdentityModel.Tokens.Saml2;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A signed assertion a sign-in can rest on names its subject, states an authentication and carries exactly one
    /// bearer confirmation with its data and its conditions. Each is changed before signing, so the signature still
    /// holds and only the content is wrong.
    /// </summary>
    [TestClass]
    public class SamlAssertionContentTests
    {
        private const string HolderOfKeyMethod = "urn:oasis:names:tc:SAML:2.0:cm:holder-of-key";

        [TestMethod]
        public void Validate_NoAuthnStatement_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.EditAssertionBeforeSigning = assertion => assertion.RemoveChild(SamlXmlEdit.AssertionChild(assertion, "AuthnStatement"));

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "0 AuthnStatements");
        }

        [TestMethod]
        public void Validate_TwoAuthnStatements_IsRefused()
        {
            // One, so the authentication time and class the rules check cannot be chosen among several.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.EditAssertionBeforeSigning = assertion =>
            {
                XmlElement statement = SamlXmlEdit.AssertionChild(assertion, "AuthnStatement");
                assertion.InsertAfter(statement.CloneNode(true), statement);
            };

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "2 AuthnStatements");
        }

        [TestMethod]
        public void Validate_NoNameId_ReturnsNoNameIdForTheIdentitySourceToJudge()
        {
            // Whether the identity is a NameID or an attribute is the identity provider's identity source's to decide, so
            // the validator returns the assertion without one; SamlAuthAssertionConsumerTests covers the refusal.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.EditAssertionBeforeSigning = assertion => SamlXmlEdit.AssertionChild(assertion, "Subject").RemoveChild(SamlXmlEdit.NameId(assertion));

            SamlValidatedAssertion result = harness.Validate(response.ToFormValue());

            Assert.IsNull(result.NameId);
        }

        [TestMethod]
        public void Validate_OnlyHolderOfKeyConfirmation_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.EditAssertionBeforeSigning = assertion => Confirmation(assertion).SetAttribute("Method", HolderOfKeyMethod);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "0 bearer confirmations");
        }

        [TestMethod]
        public void Validate_TwoBearerConfirmations_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.EditAssertionBeforeSigning = assertion =>
            {
                XmlElement confirmation = Confirmation(assertion);
                confirmation.ParentNode!.InsertAfter(confirmation.CloneNode(true), confirmation);
            };

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "2 bearer confirmations");
        }

        [TestMethod]
        public void Validate_BearerConfirmationWithoutData_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.EditAssertionBeforeSigning = assertion =>
            {
                XmlElement confirmation = Confirmation(assertion);
                confirmation.RemoveChild(SamlXmlEdit.AssertionChild(confirmation, "SubjectConfirmationData"));
            };

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "SubjectConfirmationData Not Found");
        }

        [TestMethod]
        public void Validate_NoConditions_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.EditAssertionBeforeSigning = assertion => assertion.RemoveChild(SamlXmlEdit.AssertionChild(assertion, "Conditions"));

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<Saml2SecurityTokenException>(rejection);
        }

        private static XmlElement Confirmation(XmlElement assertion)
        {
            return SamlXmlEdit.AssertionChild(SamlXmlEdit.AssertionChild(assertion, "Subject"), "SubjectConfirmation");
        }
    }
}
