using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What the package reads from an identity provider's metadata XML: its entity id, its HTTP-Redirect single sign-on
    /// URL, its RSA signing certificates and whether it wants signed requests. Parsed once per distinct XML and cached by
    /// its SHA-256, so resolving an unchanged configuration does not parse again and a changed one takes effect at once.
    /// </summary>
    internal sealed class SamlIdentityProviderMetadata
    {
        /// <summary>
        /// The most distinct metadata documents kept parsed. Past it the cache starts again, which only costs a parse.
        /// </summary>
        internal const int MaxCachedDocuments = 256;

        private const string RedirectBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect";
        private const string MetadataNamespace = "urn:oasis:names:tc:SAML:2.0:metadata";

        private static readonly ConcurrentDictionary<string, ParseResult> _cache = new ConcurrentDictionary<string, ParseResult>(StringComparer.Ordinal);

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
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metadataXml)));
            if (!_cache.TryGetValue(key, out ParseResult? result))
            {
                result = ParseUncached(metadataXml);
                if (_cache.Count >= MaxCachedDocuments)
                {
                    _cache.Clear();
                }

                _cache[key] = result;
            }

            problems = result.Problems;
            return result.Metadata;
        }

        private static ParseResult ParseUncached(string metadataXml)
        {
            List<string> problems = new List<string>();

            // An aggregate (EntitiesDescriptor) holds many identity providers, and ITfoxtec would take the first; one
            // provider's own EntityDescriptor leaves no doubt which entity is trusted.
            if (!IsSingleEntityDescriptor(metadataXml, problems))
            {
                return new ParseResult(null, problems);
            }

            EntityDescriptor descriptor;
            try
            {
                descriptor = new EntityDescriptor().ReadIdPSsoDescriptor(metadataXml);
            }
            catch (Exception exception) when (exception is Saml2RequestException or XmlException or CryptographicException or FormatException or UriFormatException)
            {
                problems.Add($"The metadata could not be read: {exception.Message}");
                return new ParseResult(null, problems);
            }

            if (string.IsNullOrWhiteSpace(descriptor.EntityId))
            {
                problems.Add("The metadata's EntityDescriptor has no entityID.");
            }

            IdPSsoDescriptor? idp = descriptor.IdPSsoDescriptor;
            if (idp == null)
            {
                problems.Add("The metadata has no IDPSSODescriptor.");
                return new ParseResult(null, problems);
            }

            Uri? singleSignOnUrl = (idp.SingleSignOnServices ?? [])
                .Where(service => service.Binding?.OriginalString == RedirectBinding)
                .Select(service => service.Location)
                .FirstOrDefault(location => location != null && location.IsAbsoluteUri);
            if (singleSignOnUrl == null)
            {
                problems.Add("The metadata has no SingleSignOnService with the HTTP-Redirect binding and an absolute Location.");
            }

            List<X509Certificate2> signingCertificates = (idp.SigningCertificates ?? [])
                .Where(certificate => certificate.GetRSAPublicKey() != null)
                .ToList();
            if (signingCertificates.Count == 0)
            {
                problems.Add("The metadata has no signing KeyDescriptor with an RSA certificate.");
            }

            if (problems.Count > 0)
            {
                return new ParseResult(null, problems);
            }

            SamlIdentityProviderMetadata metadata = new SamlIdentityProviderMetadata(descriptor.EntityId, singleSignOnUrl!, signingCertificates, idp.WantAuthnRequestsSigned == true);
            return new ParseResult(metadata, problems);
        }

        private static bool IsSingleEntityDescriptor(string metadataXml, List<string> problems)
        {
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using StringReader stringReader = new StringReader(metadataXml);
                using XmlReader reader = XmlReader.Create(stringReader, settings);
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

        private sealed record ParseResult(SamlIdentityProviderMetadata? Metadata, IReadOnlyList<string> Problems);
    }
}
