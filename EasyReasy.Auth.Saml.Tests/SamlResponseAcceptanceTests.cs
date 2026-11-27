using System.Security.Cryptography.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The responses a correct identity provider sends are accepted, so every refusal in the hostile tests is down to
    /// the one thing each test made wrong.
    /// </summary>
    [TestClass]
    public class SamlResponseAcceptanceTests
    {
        [TestMethod]
        public void Validate_SignedAssertion_ReturnsTheAssertionsSubject()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond("user-1234");
            response.NameQualifier = harness.IdentityProvider.EntityId;
            response.ServiceProviderNameQualifier = SamlValidationHarness.ServiceProviderEntityId;

            SamlValidatedAssertion result = harness.Validate(response.ToFormValue());

            Assert.AreEqual(response.AssertionId, result.AssertionId);
            Assert.AreEqual(harness.IdentityProvider.EntityId, result.IdentityProviderEntityId);
            Assert.AreEqual("user-1234", result.NameId);
            Assert.AreEqual(FakeSamlResponse.PersistentNameIdFormat, result.NameIdFormat);
            Assert.AreEqual(harness.IdentityProvider.EntityId, result.NameQualifier);
            Assert.AreEqual(SamlValidationHarness.ServiceProviderEntityId, result.ServiceProviderNameQualifier);
        }

        [TestMethod]
        public void Validate_SignedAssertionInsideSignedResponse_IsAccepted()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SignResponse = true;

            SamlValidatedAssertion result = harness.Validate(response.ToFormValue());

            Assert.AreEqual("user-1234", result.NameId);
        }

        [TestMethod]
        [DataRow(SignedXml.XmlDsigRSASHA384Url, SignedXml.XmlDsigSHA384Url)]
        [DataRow(SignedXml.XmlDsigRSASHA512Url, SignedXml.XmlDsigSHA512Url)]
        public void Validate_StrongerAllowedAlgorithms_AreAccepted(string signatureMethod, string digestMethod)
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SignatureMethod = signatureMethod;
            response.DigestMethod = digestMethod;

            SamlValidatedAssertion result = harness.Validate(response.ToFormValue());

            Assert.AreEqual("user-1234", result.NameId);
        }

        [TestMethod]
        public void Validate_AssertionSignedWithRolloverCertificate_IsAcceptedWhileBothArePinned()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.SigningCertificate = SamlTestCertificates.IdentityProviderRollover;
            SamlIdentityProvider provider = new SamlIdentityProvider(
                harness.IdentityProvider.MetadataXml(SamlTestCertificates.IdentityProvider, SamlTestCertificates.IdentityProviderRollover));

            SamlValidatedAssertion result = harness.Validator.Validate(response.ToFormValue(), harness.RequestId, provider);

            Assert.AreEqual("user-1234", result.NameId);
        }

        [TestMethod]
        [DataRow(SamlAlgorithms.Aes128Gcm)]
        [DataRow(SamlAlgorithms.Aes256Gcm)]
        public void Validate_EncryptedSignedAssertion_IsDecryptedAndAccepted(string dataEncryptionMethod)
        {
            SamlValidationHarness harness = new SamlValidationHarness(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);
            FakeSamlResponse response = harness.Respond();
            response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption);
            response.DataEncryptionMethod = dataEncryptionMethod;

            SamlValidatedAssertion result = harness.Validate(response.ToFormValue());

            Assert.AreEqual("user-1234", result.NameId);
        }
    }
}
