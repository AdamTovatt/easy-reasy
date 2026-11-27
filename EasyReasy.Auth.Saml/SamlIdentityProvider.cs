using System.Security.Cryptography.X509Certificates;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// One SAML identity provider as the application trusts it, returned by
    /// <see cref="ISamlIdentityProviderResolver"/> per request: its metadata XML, which the application supplies and
    /// refreshes (the package never fetches it), and where its assertions carry the person's identity. Valid by
    /// construction: the constructor reads the metadata and refuses anything the package could not use.
    /// </summary>
    /// <remarks>
    /// The metadata's signing certificates are the pins: an assertion is accepted only with a signature by one of them,
    /// and several are accepted at once so the identity provider can roll its key over. Metadata the application fetched
    /// itself should come over a channel it trusts, or have its own signature checked first, since whoever controls the
    /// XML controls whose assertions are accepted. Parsed metadata is cached by the XML's SHA-256.
    /// </remarks>
    public sealed class SamlIdentityProvider
    {
        private readonly SamlIdentityProviderMetadata _metadata;

        /// <summary>
        /// Initializes an identity provider from its metadata.
        /// </summary>
        /// <param name="metadataXml">The identity provider's metadata: one <c>md:EntityDescriptor</c> with an
        /// <c>IDPSSODescriptor</c>, an HTTP-Redirect <c>SingleSignOnService</c> and at least one RSA signing
        /// certificate.</param>
        /// <param name="identitySource">Where its assertions carry the identity; defaults to
        /// <see cref="SamlIdentitySource.PersistentNameId"/>.</param>
        /// <exception cref="ArgumentException">The metadata is unusable. Every problem is reported in one exception.</exception>
        public SamlIdentityProvider(string metadataXml, SamlIdentitySource? identitySource = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metadataXml);

            SamlIdentityProviderMetadata? metadata = SamlIdentityProviderMetadata.Parse(metadataXml, out IReadOnlyList<string> problems);
            if (metadata == null)
            {
                throw new ArgumentException($"Invalid identity provider metadata:\n{string.Join("\n", problems)}", nameof(metadataXml));
            }

            _metadata = metadata;
            IdentitySource = identitySource ?? SamlIdentitySource.PersistentNameId;
        }

        /// <summary>
        /// The identity provider's entity id, from its metadata. Every response and assertion must be issued under it.
        /// </summary>
        public string EntityId => _metadata.EntityId;

        /// <summary>
        /// The HTTP-Redirect single sign-on URL AuthnRequests are sent to, from its metadata.
        /// </summary>
        public Uri SingleSignOnUrl => _metadata.SingleSignOnUrl;

        /// <summary>
        /// The pinned certificates whose keys may sign assertions, from its metadata.
        /// </summary>
        public IReadOnlyList<X509Certificate2> SigningCertificates => _metadata.SigningCertificates;

        /// <summary>
        /// Whether its metadata sets <c>WantAuthnRequestsSigned</c>. Beginning a sign-in with it then requires
        /// <see cref="SamlAuthOptions.SigningCertificate"/>.
        /// </summary>
        public bool WantsSignedRequests => _metadata.WantsSignedRequests;

        /// <summary>
        /// Where its assertions carry the person's identity.
        /// </summary>
        public SamlIdentitySource IdentitySource { get; }
    }
}
