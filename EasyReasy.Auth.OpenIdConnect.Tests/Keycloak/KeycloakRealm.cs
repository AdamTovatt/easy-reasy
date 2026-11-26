namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// A realm <see cref="KeycloakServer.CreateRealmAsync"/> set up: its name, its issuer, the application client's
    /// secret and its one user's id, which is the <c>sub</c> of that user's ID tokens.
    /// </summary>
    internal sealed class KeycloakRealm
    {
        public KeycloakRealm(string name, string issuer, string clientSecret, string userId)
        {
            Name = name;
            Issuer = issuer;
            ClientSecret = clientSecret;
            UserId = userId;
        }

        public string Name { get; }

        public string Issuer { get; }

        public string ClientSecret { get; }

        public string UserId { get; }
    }
}
