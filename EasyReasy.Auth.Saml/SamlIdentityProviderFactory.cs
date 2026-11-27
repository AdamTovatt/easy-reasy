using System.Collections.Concurrent;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Builds the <see cref="SamlIdentityProvider"/>s an <see cref="ISamlIdentityProviderResolver"/> returns, from the
    /// metadata XML the application supplies. A singleton
    /// <see cref="SamlAuthServiceCollectionExtensions.AddEasyReasySamlAuth"/> registers, for the resolver to inject.
    /// </summary>
    /// <remarks>
    /// Metadata is parsed once per distinct XML and kept by its hash, so a resolver can build the provider on every call:
    /// an unchanged configuration is not parsed again, and a changed one takes effect at once. What is kept belongs to
    /// this factory, so nothing is shared beyond the application it serves.
    /// </remarks>
    public sealed class SamlIdentityProviderFactory
    {
        /// <summary>
        /// The most distinct metadata documents kept parsed. Past it the factory starts again, which only costs a parse.
        /// </summary>
        internal const int MaxCachedDocuments = 256;

        private readonly ConcurrentDictionary<string, SamlIdentityProviderMetadata> _parsed = new ConcurrentDictionary<string, SamlIdentityProviderMetadata>(StringComparer.Ordinal);

        /// <summary>
        /// Builds an identity provider from its metadata.
        /// </summary>
        /// <param name="metadataXml">The identity provider's metadata: one <c>md:EntityDescriptor</c> with an
        /// <c>IDPSSODescriptor</c>, an HTTP-Redirect <c>SingleSignOnService</c> and at least one RSA signing
        /// certificate.</param>
        /// <param name="identitySource">Where its assertions carry the identity; defaults to
        /// <see cref="SamlIdentitySource.PersistentNameId"/>.</param>
        /// <returns>The identity provider.</returns>
        /// <exception cref="ArgumentException"><paramref name="metadataXml"/> is null, blank or unusable; every problem is
        /// reported in one exception, and <see cref="SamlIdentityProvider.FindMetadataProblems"/> lists them.</exception>
        public SamlIdentityProvider Create(string metadataXml, SamlIdentitySource? identitySource = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(metadataXml);

            return new SamlIdentityProvider(Parsed(metadataXml), identitySource ?? SamlIdentitySource.PersistentNameId);
        }

        private SamlIdentityProviderMetadata Parsed(string metadataXml)
        {
            string key = SecureToken.Hash(metadataXml);
            if (_parsed.TryGetValue(key, out SamlIdentityProviderMetadata? metadata))
            {
                return metadata;
            }

            metadata = SamlIdentityProviderMetadata.Read(metadataXml);
            if (_parsed.Count >= MaxCachedDocuments)
            {
                _parsed.Clear();
            }

            _parsed[key] = metadata;
            return metadata;
        }
    }
}
