using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Cryptography;
using System.Security.Cryptography;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Signature wrapping: an attacker holds a validly signed response for their own account and moves the signed
    /// assertion where the signature check still finds it, while a forged assertion about the victim sits where the
    /// identity is read. Every variant must be refused, and the identity must never come from the forgery.
    /// </summary>
    [TestClass]
    public class SamlSignatureWrappingTests
    {
        private const string Attacker = "attacker-1";
        private const string Victim = "victim-1";

        [TestMethod]
        public void Validate_UnsignedForgeryBesideOriginalMovedIntoExtensions_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            XmlDocument document = harness.Respond(Attacker).BuildDocument();
            XmlElement original = SamlXmlEdit.Assertion(document);
            XmlElement forged = SamlXmlEdit.ForgeAssertion(original, Victim, "_forged", keepSignature: false);
            document.DocumentElement!.ReplaceChild(forged, original);
            SamlXmlEdit.AddExtensions(document).AppendChild(original);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Signature is invalid");
        }

        [TestMethod]
        public void Validate_ForgeryCarryingOriginalsSignatureWithOriginalInExtensions_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            XmlDocument document = harness.Respond(Attacker).BuildDocument();
            XmlElement original = SamlXmlEdit.Assertion(document);
            XmlElement forged = SamlXmlEdit.ForgeAssertion(original, Victim, "_forged", keepSignature: true);
            document.DocumentElement!.ReplaceChild(forged, original);
            SamlXmlEdit.AddExtensions(document).AppendChild(original);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "do not refer to the root element");
        }

        [TestMethod]
        public void Validate_ForgeryCarryingOriginalsSignatureWithOriginalInsideThatSignature_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            XmlDocument document = harness.Respond(Attacker).BuildDocument();
            XmlElement original = SamlXmlEdit.Assertion(document);
            XmlElement forged = SamlXmlEdit.ForgeAssertion(original, Victim, "_forged", keepSignature: true);
            document.DocumentElement!.ReplaceChild(forged, original);
            SamlXmlEdit.AppendToSignatureObject(SamlXmlEdit.Signature(forged), original);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "do not refer to the root element");
        }

        [TestMethod]
        public void Validate_ForgeryReusingOriginalsIdWithOriginalInExtensions_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond(Attacker);
            XmlDocument document = response.BuildDocument();
            XmlElement original = SamlXmlEdit.Assertion(document);
            XmlElement forged = SamlXmlEdit.ForgeAssertion(original, Victim, response.AssertionId, keepSignature: true);
            document.DocumentElement!.ReplaceChild(forged, original);
            SamlXmlEdit.AddExtensions(document).AppendChild(original);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<CryptographicException>(rejection, "Malformed reference element");
        }

        [TestMethod]
        public void Validate_UnsignedForgeryBesideOriginalBothDirectlyInResponse_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            XmlDocument document = harness.Respond(Attacker).BuildDocument();
            XmlElement original = SamlXmlEdit.Assertion(document);
            XmlElement forged = SamlXmlEdit.ForgeAssertion(original, Victim, "_forged", keepSignature: false);
            document.DocumentElement!.InsertBefore(forged, original);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "not exactly one SAML Assertion");
        }

        [TestMethod]
        public void Validate_SignedResponseWrappedInForgedResponse_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond(Attacker);
            response.SignResponse = true;
            XmlDocument original = response.BuildDocument();

            // A new response root carries the original's signature and an unsigned assertion about the victim, with
            // the original signed response tucked into its Extensions.
            XmlDocument document = new XmlDocument { PreserveWhitespace = true };
            XmlElement originalRoot = (XmlElement)document.ImportNode(original.DocumentElement!, true);
            XmlElement forgedRoot = (XmlElement)originalRoot.CloneNode(true);
            forgedRoot.SetAttribute("ID", "_forged-response");
            document.AppendChild(forgedRoot);
            XmlElement forgedAssertion = SamlXmlEdit.Assertion(document);
            SamlXmlEdit.NameId(forgedAssertion).InnerText = Victim;
            forgedAssertion.RemoveChild(SamlXmlEdit.Signature(forgedAssertion));
            SamlXmlEdit.AddExtensions(document).AppendChild(originalRoot);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "do not refer to the root element");
        }

        [TestMethod]
        public void Validate_UnsignedAssertionInsideResponseSignatureBesideUndecryptableOne_IsRefused()
        {
            // The case ITfoxtec 4.22.2 fixed: the enveloped-signature transform leaves ds:Signature out of the digest,
            // so an assertion added inside the response's signature keeps that signature valid, and 4.22.0 read it
            // when the genuine assertion was encrypted for a key this service provider does not hold.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond(Attacker);
            response.SignResponse = true;
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            XmlDocument document = response.BuildDocument();
            SamlXmlEdit.AppendToSignatureObject(SamlXmlEdit.Signature(document.DocumentElement!), ForgeUnsignedAssertion(harness, Victim));

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "not exactly one SAML Assertion");
        }

        [TestMethod]
        public void Validate_UnsignedAssertionInsideResponseSignatureBesideDecryptableOne_ReadsTheSignedAssertion()
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond(Attacker);
            response.SignResponse = true;
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            XmlDocument document = response.BuildDocument();
            SamlXmlEdit.AppendToSignatureObject(SamlXmlEdit.Signature(document.DocumentElement!), ForgeUnsignedAssertion(harness, Victim));

            SamlValidatedAssertion result = harness.Validate(FakeSamlResponse.Encode(document));

            Assert.AreEqual(Attacker, result.NameId);
            Assert.AreEqual(response.AssertionId, result.AssertionId);
        }

        private static XmlElement ForgeUnsignedAssertion(SamlValidationHarness harness, string nameId)
        {
            FakeSamlResponse forged = harness.Respond(nameId);
            forged.SignAssertion = false;
            return SamlXmlEdit.Assertion(forged.BuildDocument());
        }
    }
}
