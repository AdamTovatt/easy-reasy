namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A realm <see cref="KeycloakServer.CreateRealmAsync"/> set up: its name, its entity id and its SAML metadata.
    /// </summary>
    internal sealed class KeycloakRealm
    {
        public KeycloakRealm(string name, string entityId, string metadataXml)
        {
            Name = name;
            EntityId = entityId;
            MetadataXml = metadataXml;
        }

        public string Name { get; }

        public string EntityId { get; }

        public string MetadataXml { get; }
    }
}
