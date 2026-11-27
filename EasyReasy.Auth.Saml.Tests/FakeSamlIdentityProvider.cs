using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An in-process identity provider: an entity id, a single sign-on URL and a signing key, publishing metadata an app
    /// trusts it from and answering an AuthnRequest with a response the validator accepts until a test changes it.
    /// </summary>
    internal sealed class FakeSamlIdentityProvider
    {
        /// <summary>
        /// The persistent NameID a response carries unless a test names another.
        /// </summary>
        public const string DefaultNameId = "user-1234";

        private static readonly XNamespace _metadataNamespace = "urn:oasis:names:tc:SAML:2.0:metadata";
        private static readonly XNamespace _signatureNamespace = "http://www.w3.org/2000/09/xmldsig#";

        public FakeSamlIdentityProvider(string entityId, X509Certificate2 signingCertificate)
        {
            EntityId = entityId;
            SigningCertificate = signingCertificate;
            SingleSignOnUrl = new Uri(new Uri(entityId), "/sso");
        }

        public string EntityId { get; }

        public Uri SingleSignOnUrl { get; }

        public X509Certificate2 SigningCertificate { get; }

        /// <summary>
        /// Gets this identity provider as an app configures it: its metadata, pinning its signing certificate.
        /// </summary>
        public ConfiguredSamlIdentityProvider Configured => new ConfiguredSamlIdentityProvider(MetadataXml());

        /// <summary>
        /// Gets this identity provider's metadata as the package parses it.
        /// </summary>
        public SamlIdentityProviderMetadata Metadata => SamlIdentityProviderMetadata.Read(MetadataXml());

        /// <summary>
        /// Returns the identity provider's metadata, with <paramref name="signingCertificates"/> as its signing keys (by
        /// default its own) and only their public parts.
        /// </summary>
        public string MetadataXml(params X509Certificate2[] signingCertificates)
        {
            return MetadataXml(wantsSignedRequests: false, signingCertificates);
        }

        /// <summary>
        /// Returns the identity provider's metadata, setting <c>WantAuthnRequestsSigned</c> as asked.
        /// </summary>
        public string MetadataXml(bool wantsSignedRequests, params X509Certificate2[] signingCertificates)
        {
            IEnumerable<X509Certificate2> certificates = signingCertificates.Length == 0 ? [SigningCertificate] : signingCertificates;
            XElement descriptor = new XElement(_metadataNamespace + "IDPSSODescriptor",
                new XAttribute("protocolSupportEnumeration", "urn:oasis:names:tc:SAML:2.0:protocol"),
                certificates.Select(certificate => new XElement(_metadataNamespace + "KeyDescriptor",
                    new XAttribute("use", "signing"),
                    new XElement(_signatureNamespace + "KeyInfo",
                        new XElement(_signatureNamespace + "X509Data",
                            new XElement(_signatureNamespace + "X509Certificate", Convert.ToBase64String(certificate.Export(X509ContentType.Cert))))))),
                new XElement(_metadataNamespace + "NameIDFormat", FakeSamlResponse.PersistentNameIdFormat),
                new XElement(_metadataNamespace + "SingleSignOnService",
                    new XAttribute("Binding", "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect"),
                    new XAttribute("Location", SingleSignOnUrl.AbsoluteUri)));
            if (wantsSignedRequests)
            {
                descriptor.Add(new XAttribute("WantAuthnRequestsSigned", "true"));
            }

            XElement entity = new XElement(_metadataNamespace + "EntityDescriptor", new XAttribute("entityID", EntityId), descriptor);
            return entity.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Answers the request <paramref name="requestId"/> from <paramref name="serviceProvider"/> for the user
        /// <paramref name="nameId"/>, valid from now on the real clock, as the library checks some times against it.
        /// </summary>
        public FakeSamlResponse Respond(string requestId, SamlAuthOptions serviceProvider, string nameId = DefaultNameId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string assertionConsumerServiceUrl = serviceProvider.AssertionConsumerServiceUrl.AbsoluteUri;
            return new FakeSamlResponse
            {
                InResponseTo = requestId,
                ConfirmationInResponseTo = requestId,
                Destination = assertionConsumerServiceUrl,
                Recipient = assertionConsumerServiceUrl,
                ResponseIssuer = EntityId,
                AssertionIssuer = EntityId,
                AudienceRestrictions = [[serviceProvider.ServiceProviderEntityId]],
                IssueInstant = now,
                ConditionsNotBefore = now.AddMinutes(-1),
                ConditionsNotOnOrAfter = now.AddMinutes(5),
                ConfirmationNotOnOrAfter = now.AddMinutes(5),
                NameId = nameId,
                SigningCertificate = SigningCertificate,
            };
        }
    }
}
