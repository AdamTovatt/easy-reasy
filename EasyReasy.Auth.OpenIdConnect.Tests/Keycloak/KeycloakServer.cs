using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The OpenID Connect realms the integration tests sign in against, each created through <see cref="KeycloakAdmin"/>
    /// with one user and one confidential client for the application.
    /// </summary>
    internal static class KeycloakServer
    {
        public const string Username = "oidc-user";
        public const string Password = "oidc-user-password";
        public const string ClientId = "easyreasy-app";

        /// <summary>
        /// The email the realm's one user has.
        /// </summary>
        public static string Email => KeycloakAdmin.EmailOf(Username);

        /// <summary>
        /// Creates a realm with one user and one confidential client for the application: the authorization code flow
        /// only, PKCE with S256 required, and <paramref name="redirectUri"/> as its one redirect URI.
        /// </summary>
        public static async Task<KeycloakRealm> CreateRealmAsync(string redirectUri)
        {
            string clientSecret = SecureToken.Generate();
            using KeycloakAdmin admin = await KeycloakAdmin.CreateRealmAsync("oidc-", Username, Password, "Oidc");

            await admin.PostToRealmAsync("clients", new
            {
                clientId = ClientId,
                protocol = "openid-connect",
                enabled = true,
                publicClient = false,
                secret = clientSecret,
                standardFlowEnabled = true,
                directAccessGrantsEnabled = false,
                implicitFlowEnabled = false,
                redirectUris = new[] { redirectUri },
                attributes = new Dictionary<string, string> { ["pkce.code.challenge.method"] = "S256" },
            });

            using JsonDocument users = await admin.GetFromRealmAsync($"users?username={Username}&exact=true");
            string userId = users.RootElement[0].GetProperty("id").GetString() ?? throw new AssertFailedException("Keycloak returned a user without an id.");

            return new KeycloakRealm(admin.Realm, admin.RealmUrl, clientSecret, userId);
        }
    }
}
