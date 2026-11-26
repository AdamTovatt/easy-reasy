using System.Security.Cryptography.X509Certificates;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// This app as a SAML service provider: its one entity id, its one assertion consumer service URL, and the keys it
    /// decrypts assertions with, if any.
    /// </summary>
    internal sealed class SamlServiceProviderSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamlServiceProviderSettings"/> class.
        /// </summary>
        /// <param name="entityId">The service provider's entity id, which every audience restriction must name.</param>
        /// <param name="assertionConsumerServiceUrl">The URL the identity provider posts responses to, which the response's destination and the bearer confirmation's recipient must equal.</param>
        /// <param name="decryptionCertificates">Certificates with private keys that decrypt assertions. When any is given, a plaintext assertion is refused.</param>
        public SamlServiceProviderSettings(string entityId, Uri assertionConsumerServiceUrl, IReadOnlyList<X509Certificate2> decryptionCertificates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
            ArgumentNullException.ThrowIfNull(assertionConsumerServiceUrl);
            ArgumentNullException.ThrowIfNull(decryptionCertificates);
            if (!assertionConsumerServiceUrl.IsAbsoluteUri)
            {
                throw new ArgumentException("The assertion consumer service URL must be absolute.", nameof(assertionConsumerServiceUrl));
            }

            EntityId = entityId;
            AssertionConsumerServiceUrl = assertionConsumerServiceUrl;
            DecryptionCertificates = decryptionCertificates;
        }

        /// <summary>
        /// Gets the service provider's entity id.
        /// </summary>
        public string EntityId { get; }

        /// <summary>
        /// Gets the URL the identity provider posts responses to.
        /// </summary>
        public Uri AssertionConsumerServiceUrl { get; }

        /// <summary>
        /// Gets the certificates that decrypt assertions; empty when assertions arrive in plaintext.
        /// </summary>
        public IReadOnlyList<X509Certificate2> DecryptionCertificates { get; }
    }
}
