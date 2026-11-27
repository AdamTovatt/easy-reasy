namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// One SAML identity provider as the application trusts it, returned by <see cref="ISamlIdentityProviderResolver"/>
    /// per request: its metadata, which the application supplies and refreshes (the package never fetches it), and where
    /// its assertions carry the person's identity. Built by <see cref="SamlIdentityProviderFactory.Create"/>, which refuses
    /// metadata the package cannot use, so every identity provider can be signed in with.
    /// </summary>
    /// <remarks>
    /// The metadata's signing certificates are the pins: an assertion is accepted only with a signature by one of them,
    /// and several are accepted at once so the identity provider can roll its key over. Metadata the application fetched
    /// itself should come over a channel it trusts, or have its own signature checked first, since whoever controls the
    /// XML controls whose assertions are accepted.
    /// </remarks>
    public sealed class SamlIdentityProvider
    {
        internal SamlIdentityProvider(SamlIdentityProviderMetadata metadata, SamlIdentitySource identitySource)
        {
            Metadata = metadata;
            IdentitySource = identitySource;
        }

        /// <summary>
        /// Where its assertions carry the person's identity.
        /// </summary>
        public SamlIdentitySource IdentitySource { get; }

        /// <summary>
        /// What the package read from the metadata.
        /// </summary>
        internal SamlIdentityProviderMetadata Metadata { get; }

        /// <summary>
        /// Every reason the package could not use <paramref name="metadataXml"/>, for an application to report when it
        /// accepts an identity provider's metadata, so its resolver is never handed metadata
        /// <see cref="SamlIdentityProviderFactory.Create"/> refuses.
        /// </summary>
        /// <param name="metadataXml">The metadata XML.</param>
        /// <returns>Every problem; empty when the metadata is usable.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="metadataXml"/> is null.</exception>
        public static IReadOnlyList<string> FindMetadataProblems(string metadataXml)
        {
            ArgumentNullException.ThrowIfNull(metadataXml);

            SamlIdentityProviderMetadata.Parse(metadataXml, out IReadOnlyList<string> problems);
            return problems;
        }
    }
}
