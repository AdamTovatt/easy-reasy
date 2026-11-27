namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An identity provider as an application keeps it configured: its metadata XML and identity source, which
    /// <see cref="FakeSamlIdentityProviderResolver"/> builds a <see cref="SamlIdentityProvider"/> from on every call.
    /// </summary>
    internal sealed record ConfiguredSamlIdentityProvider(string MetadataXml, SamlIdentitySource? IdentitySource = null);
}
