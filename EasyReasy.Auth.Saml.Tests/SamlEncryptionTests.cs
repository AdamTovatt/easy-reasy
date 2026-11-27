using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// When the service provider decrypts assertions, a plaintext one is refused, so an attacker cannot downgrade the
    /// identity provider's encryption away.
    /// </summary>
    [TestClass]
    public class SamlEncryptionTests
    {
        [TestMethod]
        public void Validate_PlaintextAssertionWhenDecrypting_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);

            SamlResponseRejectedException rejection = harness.Reject(harness.Respond().ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "arrived in plaintext");
        }

        [TestMethod]
        public void Validate_EncryptedAssertionWithoutDecryptionKey_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "not exactly one SAML Assertion");
        }

        [TestMethod]
        public void Validate_AssertionEncryptedForAnotherKey_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.Attacker);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<AggregateException>(rejection, "Failed to decrypt");
            // Whether decryption or a signature failed is what a decryption oracle reads, so only the inner exception says.
            Assert.AreEqual("ITfoxtec refused the response.", rejection.Message);
        }

        [TestMethod]
        public void Validate_AssertionEncryptedWithAesCbc_IsRefusedBeforeDecrypting()
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            response.DataEncryptionMethod = EncryptedXml.XmlEncAES256Url;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "data encryption method");
        }

        [TestMethod]
        public void Validate_KeyWrappedWithXmlEncryption11RsaOaep_IsDecryptedAndAccepted()
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            response.KeyTransportMethod = SamlAlgorithms.RsaOaep11;

            SamlValidatedAssertion assertion = harness.Validate(response.ToFormValue());

            Assert.AreEqual(FakeSamlIdentityProvider.DefaultNameId, assertion.NameId);
        }

        [TestMethod]
        public void Validate_KeyWrappedWithXmlEncryption11RsaOaepNamingSha1_IsDecryptedAndAccepted()
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            response.KeyTransportMethod = SamlAlgorithms.RsaOaep11;
            response.Oaep11DigestMethod = SignedXml.XmlDsigSHA1Url;
            response.Oaep11MaskGenerationFunction = FakeXmlEncryptor.Mgf1Sha1;

            SamlValidatedAssertion assertion = harness.Validate(response.ToFormValue());

            Assert.AreEqual(FakeSamlIdentityProvider.DefaultNameId, assertion.NameId);
        }

        [TestMethod]
        public void Validate_KeyWrappedWithXmlEncryption11RsaOaepNamingNoParameters_IsRefused()
        {
            // The specification's defaults are SHA-1 and MGF1-SHA1, but ITfoxtec reads a missing MGF as MGF1-SHA256, so a key
            // wrapped with the defaults fails to decrypt: refused, never accepted. The README asks for the parameters named.
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            response.KeyTransportMethod = SamlAlgorithms.RsaOaep11;
            response.Oaep11DigestMethod = null;
            response.Oaep11MaskGenerationFunction = null;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<AggregateException>(rejection, "Failed to decrypt");
        }

        [TestMethod]
        public void Validate_KeyWrappedWithXmlEncryption11RsaOaepNamingAnotherMaskGenerationFunction_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            response.KeyTransportMethod = SamlAlgorithms.RsaOaep11;
            response.Oaep11MaskGenerationFunction = FakeXmlEncryptor.Mgf1Sha512;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<AggregateException>(rejection, "Failed to decrypt");
        }

        [TestMethod]
        public void Validate_KeyWrappedWithRsaPkcs1V15_IsRefusedBeforeDecrypting()
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            response.KeyTransportMethod = EncryptedXml.XmlEncRSA15Url;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "key transport method");
        }

        [TestMethod]
        public void Validate_AesCbcCiphertextOutsideTheAssertion_IsRefusedBeforeDecrypting()
        {
            // ITfoxtec decrypts every EncryptedData in the document, so a probe placed in Extensions beside a valid
            // encrypted assertion is checked as well.
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            XmlDocument document = response.BuildDocument();
            XmlElement probe = SamlXmlEdit.Assertion(harness.Respond().BuildDocument());
            XmlElement encryptedProbe = FakeXmlEncryptor.Encrypt(
                probe,
                SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption),
                EncryptedXml.XmlEncAES256Url,
                EncryptedXml.XmlEncRSAOAEPUrl);
            SamlXmlEdit.AddExtensions(document).AppendChild(document.ImportNode(encryptedProbe, true));

            SamlResponseRejectedException rejection = harness.Reject(FakeSamlResponse.Encode(document));

            SamlRejectionAssert.ByPackage(rejection, "data encryption method");
        }

        [TestMethod]
        public void Validate_UnsignedEncryptedAssertion_IsRefused()
        {
            // Encryption is not a signature: anyone can encrypt for the service provider's public key.
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.SignAssertion = false;
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<InvalidSignatureException>(rejection, "Signature is invalid");
        }
    }
}
