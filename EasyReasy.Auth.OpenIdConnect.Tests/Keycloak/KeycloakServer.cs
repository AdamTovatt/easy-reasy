using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The Keycloak the integration tests sign in against, at <c>KEYCLOAK_URL</c>, with the bootstrap admin the start
    /// script creates. Each test class sets up a realm of its own through the admin API, named uniquely, so runs never see
    /// each other's state and nothing needs cleaning between them.
    /// </summary>
    /// <remarks>
    /// Started by <c>.github/scripts/keycloak.sh</c>, which sets <c>KEYCLOAK_URL</c> in CI and prints it otherwise;
    /// CLAUDE.md has the local commands.
    /// </remarks>
    internal static class KeycloakServer
    {
        public const string Username = "oidc-user";
        public const string Password = "oidc-user-password";
        public const string Email = "oidc-user@example.test";
        public const string ClientId = "easyreasy-app";

        // The bootstrap admin .github/scripts/keycloak.sh starts the container with.
        private const string AdminUsername = "admin";
        private const string AdminPassword = "admin";

        private static readonly string? ConfiguredUrl = Environment.GetEnvironmentVariable("KEYCLOAK_URL");

        /// <summary>
        /// Whether <c>KEYCLOAK_URL</c> names a Keycloak to run against.
        /// </summary>
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(ConfiguredUrl);

        private static Uri BaseUrl => string.IsNullOrWhiteSpace(ConfiguredUrl)
            ? throw new InvalidOperationException("KEYCLOAK_URL is not set.")
            : new Uri(ConfiguredUrl.TrimEnd('/') + "/");

        /// <summary>
        /// Creates a realm with one user and one confidential client for the application: the authorization code flow
        /// only, PKCE with S256 required, and <paramref name="redirectUri"/> as its one redirect URI.
        /// </summary>
        public static async Task<KeycloakRealm> CreateRealmAsync(string redirectUri)
        {
            string realm = "oidc-" + Guid.NewGuid().ToString("N")[..12];
            string clientSecret = SecureToken.Generate();
            using HttpClient admin = await CreateAdminClientAsync();

            await PostAsync(admin, "admin/realms", new { realm, enabled = true });
            await PostAsync(admin, $"admin/realms/{realm}/users", new
            {
                username = Username,
                enabled = true,
                email = Email,
                emailVerified = true,
                firstName = "Oidc",
                lastName = "User",
                credentials = new[] { new { type = "password", value = Password, temporary = false } },
            });
            await PostAsync(admin, $"admin/realms/{realm}/clients", new
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

            HttpResponseMessage usersResponse = await admin.GetAsync($"admin/realms/{realm}/users?username={Username}&exact=true");
            await EnsureSuccessAsync(usersResponse, "the user lookup");
            using JsonDocument users = JsonDocument.Parse(await usersResponse.Content.ReadAsStringAsync());
            string userId = users.RootElement[0].GetProperty("id").GetString() ?? throw new AssertFailedException("Keycloak returned a user without an id.");

            return new KeycloakRealm(realm, new Uri(BaseUrl, $"realms/{realm}").ToString(), clientSecret, userId);
        }

        private static async Task<HttpClient> CreateAdminClientAsync()
        {
            HttpClient client = new HttpClient { BaseAddress = BaseUrl };
            HttpResponseMessage tokenResponse = await client.PostAsync("realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = AdminUsername,
                ["password"] = AdminPassword,
            }));
            await EnsureSuccessAsync(tokenResponse, "the admin token");
            using JsonDocument token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("access_token").GetString());
            return client;
        }

        private static async Task PostAsync(HttpClient admin, string path, object body)
        {
            HttpResponseMessage response = await admin.PostAsJsonAsync(path, body);
            await EnsureSuccessAsync(response, path);
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, string what)
        {
            if (!response.IsSuccessStatusCode)
            {
                Assert.Fail($"Keycloak at {BaseUrl} refused {what}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            }
        }
    }
}
