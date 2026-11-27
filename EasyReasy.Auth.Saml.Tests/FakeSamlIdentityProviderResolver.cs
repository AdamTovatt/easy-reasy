namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An <see cref="ISamlIdentityProviderResolver"/> over a dictionary a test can change between Begin and the assertion
    /// consumer service.
    /// </summary>
    internal sealed class FakeSamlIdentityProviderResolver : ISamlIdentityProviderResolver
    {
        public Dictionary<string, SamlIdentityProvider> IdentityProviders { get; } = new Dictionary<string, SamlIdentityProvider>(StringComparer.Ordinal);

        public int Calls { get; private set; }

        public Task<SamlIdentityProvider?> FindIdentityProviderAsync(string identityProviderKey, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(IdentityProviders.TryGetValue(identityProviderKey, out SamlIdentityProvider? provider) ? provider : null);
        }
    }
}
