using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Identity provider metadata the application supplies is usable or refused with every problem, parsed once per
    /// distinct XML for one registration, and the service provider's metadata carries what an identity provider
    /// administrator needs and no private key.
    /// </summary>
    [TestClass]
    public class SamlMetadataTests
    {
        private static readonly FakeSamlIdentityProvider _identityProvider = new FakeSamlIdentityProvider("https://idp.example.test/metadata", SamlTestCertificates.IdentityProvider);

        [TestMethod]
        public void Read_ValidMetadata_ReadsEntityIdSingleSignOnUrlAndEveryPinnedCertificate()
        {
            SamlIdentityProviderMetadata metadata = SamlIdentityProviderMetadata.Read(_identityProvider.MetadataXml(SamlTestCertificates.IdentityProvider, SamlTestCertificates.IdentityProviderRollover));

            Assert.AreEqual(_identityProvider.EntityId, metadata.EntityId);
            Assert.AreEqual(_identityProvider.SingleSignOnUrl, metadata.SingleSignOnUrl);
            CollectionAssert.AreEqual(
                new[] { SamlTestCertificates.IdentityProvider.Thumbprint, SamlTestCertificates.IdentityProviderRollover.Thumbprint },
                metadata.SigningCertificates.Select(certificate => certificate.Thumbprint).ToArray());
            Assert.IsFalse(metadata.SigningCertificates.Any(certificate => certificate.HasPrivateKey));
            Assert.IsFalse(metadata.WantsSignedRequests);
        }

        [TestMethod]
        public void Read_WantAuthnRequestsSigned_IsRead()
        {
            SamlIdentityProviderMetadata metadata = SamlIdentityProviderMetadata.Read(_identityProvider.MetadataXml(wantsSignedRequests: true));

            Assert.IsTrue(metadata.WantsSignedRequests);
        }

        [TestMethod]
        public void Create_NoIdentitySource_TakesThePersistentNameId()
        {
            SamlIdentityProvider provider = new SamlIdentityProviderFactory().Create(_identityProvider.MetadataXml());

            Assert.AreSame(SamlIdentitySource.PersistentNameId, provider.IdentitySource);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public void Create_BlankMetadata_Throws(string metadataXml)
        {
            Assert.ThrowsException<ArgumentException>(() => new SamlIdentityProviderFactory().Create(metadataXml));
        }

        [TestMethod]
        public void Create_UnusableMetadata_ThrowsListingEveryProblem()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new SamlIdentityProviderFactory().Create("<not-metadata/>"));

            StringAssert.Contains(exception.Message, "EntityDescriptor");
        }

        [TestMethod]
        public void FindMetadataProblems_UsableMetadata_FindsNone()
        {
            Assert.AreEqual(0, SamlIdentityProvider.FindMetadataProblems(_identityProvider.MetadataXml()).Count);
        }

        [TestMethod]
        public void FindMetadataProblems_AggregateOfEntities_ReportsIt()
        {
            string aggregate = $"<md:EntitiesDescriptor xmlns:md=\"urn:oasis:names:tc:SAML:2.0:metadata\">{_identityProvider.MetadataXml()}</md:EntitiesDescriptor>";

            StringAssert.Contains(SamlIdentityProvider.FindMetadataProblems(aggregate).Single(), "EntityDescriptor");
        }

        [TestMethod]
        public void FindMetadataProblems_NoRedirectSingleSignOnAndNoSigningCertificate_ReportsBoth()
        {
            XmlDocument metadata = new XmlDocument();
            metadata.LoadXml(_identityProvider.MetadataXml());
            XmlNamespaceManager namespaces = new XmlNamespaceManager(metadata.NameTable);
            namespaces.AddNamespace("md", "urn:oasis:names:tc:SAML:2.0:metadata");
            metadata.SelectSingleNode("//md:SingleSignOnService", namespaces)!.Attributes!["Binding"]!.Value = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";
            XmlNode keyDescriptor = metadata.SelectSingleNode("//md:KeyDescriptor", namespaces)!;
            keyDescriptor.ParentNode!.RemoveChild(keyDescriptor);

            IReadOnlyList<string> problems = SamlIdentityProvider.FindMetadataProblems(metadata.OuterXml);

            Assert.AreEqual(2, problems.Count, string.Join("\n", problems));
            StringAssert.Contains(problems[0], "HTTP-Redirect");
            StringAssert.Contains(problems[1], "signing KeyDescriptor");
        }

        [TestMethod]
        [DataRow("javascript:alert(document.domain)")]
        [DataRow("http://idp.example.test/sso")]
        [DataRow("ftp://idp.example.test/sso")]
        public void FindMetadataProblems_SingleSignOnUrlTheBrowserMustNotBeSentTo_ReportsIt(string location)
        {
            string metadata = _identityProvider.MetadataXml().Replace(_identityProvider.SingleSignOnUrl.AbsoluteUri, location, StringComparison.Ordinal);

            StringAssert.Contains(SamlIdentityProvider.FindMetadataProblems(metadata).Single(), "HTTP-Redirect binding and an https Location");
        }

        [TestMethod]
        [DataRow("http://127.0.0.1:8080/metadata")]
        [DataRow("http://localhost:8080/metadata")]
        [DataRow("http://idp.localhost/metadata")]
        public void FindMetadataProblems_PlainHttpSingleSignOnOnALocalHost_FindsNone(string entityId)
        {
            FakeSamlIdentityProvider local = new FakeSamlIdentityProvider(entityId, SamlTestCertificates.IdentityProvider);

            Assert.AreEqual(0, SamlIdentityProvider.FindMetadataProblems(local.MetadataXml()).Count);
        }

        [TestMethod]
        public void FindMetadataProblems_MetadataWithDtd_ReportsIt()
        {
            string withDtd = "<!DOCTYPE md:EntityDescriptor [<!ENTITY x \"y\">]>" + _identityProvider.MetadataXml();

            StringAssert.Contains(SamlIdentityProvider.FindMetadataProblems(withDtd).Single(), "DTD");
        }

        [TestMethod]
        public void Create_SameMetadataTwice_IsParsedOnce()
        {
            SamlIdentityProviderFactory factory = new SamlIdentityProviderFactory();
            string metadata = _identityProvider.MetadataXml(SamlTestCertificates.IdentityProviderRollover);

            Assert.AreSame(factory.Create(metadata).Metadata, factory.Create(metadata).Metadata);
        }

        [TestMethod]
        public async Task Create_TwoRegistrations_ShareNothing()
        {
            await using SamlFlowHost first = await SamlFlowHost.StartAsync();
            await using SamlFlowHost second = await SamlFlowHost.StartAsync();
            string metadata = _identityProvider.MetadataXml();

            SamlIdentityProvider fromFirst = first.App.Services.GetRequiredService<SamlIdentityProviderFactory>().Create(metadata);
            SamlIdentityProvider fromSecond = second.App.Services.GetRequiredService<SamlIdentityProviderFactory>().Create(metadata);

            Assert.AreNotSame(fromFirst.Metadata, fromSecond.Metadata);
        }

        [TestMethod]
        public void Create_MoreDistinctMetadataThanItHolds_ParsesTheFirstAgain()
        {
            SamlIdentityProviderFactory factory = new SamlIdentityProviderFactory();
            string first = _identityProvider.MetadataXml(SamlTestCertificates.IdentityProvider, SamlTestCertificates.IdentityProviderRollover);
            SamlIdentityProviderMetadata before = factory.Create(first).Metadata;
            for (int index = 0; index < SamlIdentityProviderFactory.MaxCachedDocuments; index++)
            {
                factory.Create(new FakeSamlIdentityProvider($"https://idp-{index}.example.test/metadata", SamlTestCertificates.IdentityProvider).MetadataXml());
            }

            SamlIdentityProviderMetadata after = factory.Create(first).Metadata;

            Assert.AreNotSame(before, after);
            Assert.AreSame(after, factory.Create(first).Metadata);
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
        public void ServiceProviderMetadata_InvalidOptions_ThrowsListingTheProblems()
        {
            SamlAuthOptions options = new SamlAuthOptions
            {
                ServiceProviderEntityId = " ",
                AssertionConsumerServiceUrl = new Uri(SamlValidationHarness.AssertionConsumerServiceUrl),
                StateKey = new byte[32],
            };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => SamlServiceProviderMetadata.Create(options));

            StringAssert.Contains(exception.Message, nameof(SamlAuthOptions.ServiceProviderEntityId));
            Assert.AreEqual("options", exception.ParamName);
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
