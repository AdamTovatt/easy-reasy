namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An <see cref="ISamlIdentityProviderResolver"/> over a dictionary a test can change between Begin and the assertion
    /// consumer service, building each provider with the registration's factory as an application's resolver does.
    /// </summary>
    internal sealed class FakeSamlIdentityProviderResolver : ISamlIdentityProviderResolver
    {
        private readonly SamlIdentityProviderFactory _identityProviders;

        public FakeSamlIdentityProviderResolver(SamlIdentityProviderFactory identityProviders)
        {
            _identityProviders = identityProviders;
        }

        public Dictionary<string, ConfiguredSamlIdentityProvider> IdentityProviders { get; } = new Dictionary<string, ConfiguredSamlIdentityProvider>(StringComparer.Ordinal);

        public int Calls { get; private set; }

        public Task<SamlIdentityProvider?> FindIdentityProviderAsync(string identityProviderKey, CancellationToken cancellationToken)
        {
            Calls++;
            SamlIdentityProvider? provider = IdentityProviders.TryGetValue(identityProviderKey, out ConfiguredSamlIdentityProvider? configured)
                ? _identityProviders.Create(configured.MetadataXml, configured.IdentitySource)
                : null;
            return Task.FromResult(provider);
        }
    }
}
