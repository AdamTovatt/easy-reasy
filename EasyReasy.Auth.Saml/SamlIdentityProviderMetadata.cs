using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What the package reads from an identity provider's metadata XML: its entity id, its HTTP-Redirect single sign-on
    /// URL, its RSA signing certificates and whether it wants signed requests. <see cref="SamlIdentityProviderFactory"/>
    /// keeps it parsed per distinct XML.
    /// </summary>
    internal sealed class SamlIdentityProviderMetadata
    {
        private const string RedirectBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect";
        private const string MetadataNamespace = "urn:oasis:names:tc:SAML:2.0:metadata";

        private SamlIdentityProviderMetadata(string entityId, Uri singleSignOnUrl, IReadOnlyList<X509Certificate2> signingCertificates, bool wantsSignedRequests)
        {
            EntityId = entityId;
            SingleSignOnUrl = singleSignOnUrl;
            SigningCertificates = signingCertificates;
            WantsSignedRequests = wantsSignedRequests;
        }

        public string EntityId { get; }

        public Uri SingleSignOnUrl { get; }

        public IReadOnlyList<X509Certificate2> SigningCertificates { get; }

        public bool WantsSignedRequests { get; }

        /// <summary>
        /// Parses metadata, or reports every problem with it.
        /// </summary>
        /// <param name="metadataXml">The metadata XML.</param>
        /// <param name="problems">Every problem; empty when the metadata is usable.</param>
        /// <returns>The metadata, or null when there are problems.</returns>
        public static SamlIdentityProviderMetadata? Parse(string metadataXml, out IReadOnlyList<string> problems)
        {
            List<string> found = new List<string>();
            problems = found;

            // An aggregate (EntitiesDescriptor) holds many identity providers, and ITfoxtec would take the first; one
            // provider's own EntityDescriptor leaves no doubt which entity is trusted.
            if (!IsSingleEntityDescriptor(metadataXml, found))
            {
                return null;
            }

            EntityDescriptor descriptor;
            try
            {
                descriptor = new EntityDescriptor().ReadIdPSsoDescriptor(metadataXml);
            }
            catch (Exception exception) when (exception is Saml2RequestException or XmlException or CryptographicException or FormatException or UriFormatException)
            {
                found.Add($"The metadata could not be read: {exception.Message}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(descriptor.EntityId))
            {
                found.Add("The metadata's EntityDescriptor has no entityID.");
            }

            IdPSsoDescriptor? idp = descriptor.IdPSsoDescriptor;
            if (idp == null)
            {
                found.Add("The metadata has no IDPSSODescriptor.");
                return null;
            }

            Uri? singleSignOnUrl = (idp.SingleSignOnServices ?? [])
                .Where(service => service.Binding?.OriginalString == RedirectBinding)
                .Select(service => service.Location)
                .FirstOrDefault(SecureBrowserAddress.IsAllowed);
            if (singleSignOnUrl == null)
            {
                found.Add("The metadata has no SingleSignOnService with the HTTP-Redirect binding and an https Location (or http on a loopback or localhost host), the browser being sent there.");
            }

            List<X509Certificate2> signingCertificates = (idp.SigningCertificates ?? [])
                .Where(certificate => certificate.GetRSAPublicKey() != null)
                .ToList();
            if (signingCertificates.Count == 0)
            {
                found.Add("The metadata has no signing KeyDescriptor with an RSA certificate.");
            }

            return found.Count > 0
                ? null
                : new SamlIdentityProviderMetadata(descriptor.EntityId, singleSignOnUrl!, signingCertificates, idp.WantAuthnRequestsSigned == true);
        }

        /// <summary>
        /// Parses metadata, or throws reporting every problem with it.
        /// </summary>
        /// <param name="metadataXml">The metadata XML.</param>
        /// <returns>The metadata.</returns>
        /// <exception cref="ArgumentException">The metadata is unusable.</exception>
        public static SamlIdentityProviderMetadata Read(string metadataXml)
        {
            return Parse(metadataXml, out IReadOnlyList<string> problems)
                ?? throw ArgumentProblems.CreateException(problems, "identity provider metadata", nameof(metadataXml));
        }

        private static bool IsSingleEntityDescriptor(string metadataXml, List<string> problems)
        {
            try
            {
                using XmlReader reader = SamlXmlReading.CreateReader(metadataXml);
                reader.MoveToContent();
                if (reader.NamespaceURI != MetadataNamespace || reader.LocalName != "EntityDescriptor")
                {
                    problems.Add($"The metadata's root must be one md:EntityDescriptor; got {reader.LocalName}.");
                    return false;
                }

                return true;
            }
            catch (XmlException exception)
            {
                problems.Add($"The metadata is not XML without a DTD: {exception.Message}");
                return false;
            }
        }
    }
}
