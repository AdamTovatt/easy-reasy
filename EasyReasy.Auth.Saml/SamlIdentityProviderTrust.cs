using System.Security.Cryptography.X509Certificates;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What a response must match to count as coming from one identity provider: its entity id and the signing
    /// certificates pinned for it. Several certificates are accepted at once so the identity provider can roll its key
    /// over.
    /// </summary>
    internal sealed class SamlIdentityProviderTrust
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamlIdentityProviderTrust"/> class.
        /// </summary>
        /// <param name="entityId">The identity provider's entity id, which the response's and the assertion's issuer must equal.</param>
        /// <param name="signingCertificates">The certificates whose keys may sign the assertion; at least one.</param>
        public SamlIdentityProviderTrust(string entityId, IReadOnlyList<X509Certificate2> signingCertificates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
            ArgumentNullException.ThrowIfNull(signingCertificates);
            if (signingCertificates.Count == 0)
            {
                throw new ArgumentException("At least one signing certificate is required.", nameof(signingCertificates));
            }

            EntityId = entityId;
            SigningCertificates = signingCertificates;
        }

        /// <summary>
        /// Gets the identity provider's entity id.
        /// </summary>
        public string EntityId { get; }

        /// <summary>
        /// Gets the certificates whose keys may sign the assertion.
        /// </summary>
        public IReadOnlyList<X509Certificate2> SigningCertificates { get; }
    }
}
