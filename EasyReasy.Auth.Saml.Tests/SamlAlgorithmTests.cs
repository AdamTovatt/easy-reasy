using ITfoxtec.Identity.Saml2.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// SHA-1 is refused wherever it appears in a signature, and so is any algorithm outside the allowlist.
    /// </summary>
    [TestClass]
    public class SamlAlgorithmTests
    {
        private const string RsaSha1 = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
        private const string Sha1 = "http://www.w3.org/2000/09/xmldsig#sha1";

        [TestMethod]
        public void Validate_AssertionSignedWithRsaSha1_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SignatureMethod = RsaSha1;
            response.DigestMethod = Sha1;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Illegal signature method");
        }

        [TestMethod]
        public void Validate_AssertionReferenceDigestedWithSha1UnderRsaSha256_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
            response.DigestMethod = Sha1;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "digest method");
        }

        [TestMethod]
        public void Validate_ResponseSignedWithRsaSha1AroundAllowedAssertion_IsRefused()
        {
            // The response's signature is not what the package trusts, but a SHA-1 one is still refused outright.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            XmlDocument document = response.BuildDocument();
            FakeXmlSigner.Sign(document.DocumentElement!, SamlTestCertificates.IdentityProvider, RsaSha1, Sha1);

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Illegal signature method");
        }

        [TestMethod]
        public void Validate_AssertionCanonicalizedWithComments_IsRefused()
        {
            // With comments kept in the signed bytes, a comment inserted into a value would break the signature
            // instead of being ignored; refusing the method keeps one canonicalization to reason about.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.CanonicalizationMethod = SignedXml.XmlDsigExcC14NWithCommentsTransformUrl;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Illegal canonicalization method");
        }

        [TestMethod]
        public void Validate_AssertionSignatureWithTwoReferences_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SignatureReferenceCount = 2;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Invalid XML signature reference");
        }
    }
}
