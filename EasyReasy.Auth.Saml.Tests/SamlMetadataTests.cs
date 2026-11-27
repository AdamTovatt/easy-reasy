using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Identity provider metadata the application supplies is usable or refused at construction, and the service
    /// provider's metadata carries what an identity provider administrator needs and no private key.
    /// </summary>
    [TestClass]
    public class SamlMetadataTests
    {
        private static readonly FakeSamlIdentityProvider _identityProvider = new FakeSamlIdentityProvider("https://idp.example.test/metadata", SamlTestCertificates.IdentityProvider);

        [TestMethod]
        public void SamlIdentityProvider_ValidMetadata_ReadsEntityIdSingleSignOnUrlAndEveryPinnedCertificate()
        {
            SamlIdentityProvider provider = new SamlIdentityProvider(_identityProvider.MetadataXml(SamlTestCertificates.IdentityProvider, SamlTestCertificates.IdentityProviderRollover));

            Assert.AreEqual(_identityProvider.EntityId, provider.EntityId);
            Assert.AreEqual(_identityProvider.SingleSignOnUrl, provider.SingleSignOnUrl);
            CollectionAssert.AreEqual(
                new[] { SamlTestCertificates.IdentityProvider.Thumbprint, SamlTestCertificates.IdentityProviderRollover.Thumbprint },
                provider.SigningCertificates.Select(certificate => certificate.Thumbprint).ToArray());
            Assert.IsFalse(provider.SigningCertificates.Any(certificate => certificate.HasPrivateKey));
            Assert.AreSame(SamlIdentitySource.PersistentNameId, provider.IdentitySource);
            Assert.IsFalse(provider.WantsSignedRequests);
        }

        [TestMethod]
        public void SamlIdentityProvider_SameMetadataTwice_IsParsedOnce()
        {
            string metadata = _identityProvider.MetadataXml(SamlTestCertificates.IdentityProviderRollover);

            SamlIdentityProvider first = new SamlIdentityProvider(metadata);
            SamlIdentityProvider second = new SamlIdentityProvider(metadata);

            Assert.AreSame(first.SigningCertificates, second.SigningCertificates);
        }

        [TestMethod]
        public void SamlIdentityProvider_MoreDistinctMetadataThanTheCacheHolds_ParsesTheFirstAgain()
        {
            string first = _identityProvider.MetadataXml(SamlTestCertificates.IdentityProvider, SamlTestCertificates.IdentityProviderRollover);
            SamlIdentityProvider before = new SamlIdentityProvider(first);
            for (int index = 0; index < SamlIdentityProviderMetadata.MaxCachedDocuments; index++)
            {
                _ = new SamlIdentityProvider(new FakeSamlIdentityProvider($"https://idp-{index}.example.test/metadata", SamlTestCertificates.IdentityProvider).MetadataXml());
            }

            SamlIdentityProvider after = new SamlIdentityProvider(first);

            Assert.AreNotSame(before.SigningCertificates, after.SigningCertificates);
            Assert.AreSame(after.SigningCertificates, new SamlIdentityProvider(first).SigningCertificates);
        }

        [TestMethod]
        public void SamlIdentityProvider_AggregateOfEntities_IsRefused()
        {
            string aggregate = $"<md:EntitiesDescriptor xmlns:md=\"urn:oasis:names:tc:SAML:2.0:metadata\">{_identityProvider.MetadataXml()}</md:EntitiesDescriptor>";

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new SamlIdentityProvider(aggregate));

            StringAssert.Contains(exception.Message, "EntityDescriptor");
        }

        [TestMethod]
        public void SamlIdentityProvider_NoRedirectSingleSignOnAndNoSigningCertificate_ReportsBoth()
        {
            XmlDocument metadata = new XmlDocument();
            metadata.LoadXml(_identityProvider.MetadataXml());
            XmlNamespaceManager namespaces = new XmlNamespaceManager(metadata.NameTable);
            namespaces.AddNamespace("md", "urn:oasis:names:tc:SAML:2.0:metadata");
            metadata.SelectSingleNode("//md:SingleSignOnService", namespaces)!.Attributes!["Binding"]!.Value = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";
            XmlNode keyDescriptor = metadata.SelectSingleNode("//md:KeyDescriptor", namespaces)!;
            keyDescriptor.ParentNode!.RemoveChild(keyDescriptor);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new SamlIdentityProvider(metadata.OuterXml));

            StringAssert.Contains(exception.Message, "HTTP-Redirect");
            StringAssert.Contains(exception.Message, "signing");
        }

        [TestMethod]
        public void SamlIdentityProvider_MetadataWithDtd_IsRefused()
        {
            string withDtd = "<!DOCTYPE md:EntityDescriptor [<!ENTITY x \"y\">]>" + _identityProvider.MetadataXml();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new SamlIdentityProvider(withDtd));

            StringAssert.Contains(exception.Message, "DTD");
        }

        [TestMethod]
        public void SamlIdentityProvider_WantAuthnRequestsSigned_IsRead()
        {
            SamlIdentityProvider provider = new SamlIdentityProvider(_identityProvider.MetadataXml(wantsSignedRequests: true));

            Assert.IsTrue(provider.WantsSignedRequests);
        }

        [TestMethod]
        public void SamlIdentitySource_BlankAttributeName_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => SamlIdentitySource.Attribute(" "));
        }

        [TestMethod]
        public void SamlServiceProviderMetadata_Create_DescribesTheServiceProviderWithPublicCertificatesOnly()
        {
            SamlAuthOptions options = SamlValidationHarness.CreateOptions(
                [SamlTestCertificates.ServiceProviderDecryption],
                SamlTestCertificates.ServiceProviderDecryption);

            string xml = SamlServiceProviderMetadata.Create(options);

            XmlDocument metadata = new XmlDocument();
            metadata.LoadXml(xml);
            XmlNamespaceManager namespaces = new XmlNamespaceManager(metadata.NameTable);
            namespaces.AddNamespace("md", "urn:oasis:names:tc:SAML:2.0:metadata");
            namespaces.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            Assert.AreEqual(SamlValidationHarness.ServiceProviderEntityId, metadata.DocumentElement!.GetAttribute("entityID"));
            XmlElement descriptor = (XmlElement)metadata.SelectSingleNode("/md:EntityDescriptor/md:SPSSODescriptor", namespaces)!;
            Assert.AreEqual("true", descriptor.GetAttribute("WantAssertionsSigned"));
            Assert.AreEqual("true", descriptor.GetAttribute("AuthnRequestsSigned"));
            XmlElement consumer = (XmlElement)descriptor.SelectSingleNode("md:AssertionConsumerService", namespaces)!;
            Assert.AreEqual(SamlValidationHarness.AssertionConsumerServiceUrl, consumer.GetAttribute("Location"));
            Assert.AreEqual("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST", consumer.GetAttribute("Binding"));
            foreach (XmlNode certificateNode in metadata.SelectNodes("//ds:X509Certificate", namespaces)!)
            {
                X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificateNode.InnerText));
                Assert.IsFalse(certificate.HasPrivateKey);
                Assert.AreEqual(SamlTestCertificates.ServiceProviderDecryption.Thumbprint, certificate.Thumbprint);
            }

            Assert.IsNotNull(metadata.SelectSingleNode("//md:KeyDescriptor[@use='signing']", namespaces));
            Assert.IsNotNull(metadata.SelectSingleNode("//md:KeyDescriptor[@use='encryption']", namespaces));
            Assert.IsFalse(xml.Contains("PRIVATE", StringComparison.Ordinal));
        }

        [TestMethod]
        public void SamlServiceProviderMetadata_NoKeys_SaysRequestsAreUnsigned()
        {
            string xml = SamlServiceProviderMetadata.Create(SamlValidationHarness.CreateOptions([]));

            StringAssert.Contains(xml, "AuthnRequestsSigned=\"false\"");
            Assert.IsFalse(xml.Contains("KeyDescriptor", StringComparison.Ordinal));
        }
    }
}
