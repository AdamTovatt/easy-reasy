using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The admin API of the Keycloak the <c>*KeycloakIntegrationTests</c> sign in against, at <c>KEYCLOAK_URL</c>, signed
    /// in as the bootstrap admin, for one realm it created. Each test class creates realms of its own, named uniquely, so
    /// runs never see each other's state and nothing needs cleaning between them; it then adds the client its protocol
    /// needs. Linked into every test project with Keycloak tests.
    /// </summary>
    /// <remarks>
    /// Keycloak is started by <c>.github/scripts/keycloak.sh</c>, which sets <c>KEYCLOAK_URL</c> in CI and prints it
    /// otherwise; CLAUDE.md has the local commands.
    /// </remarks>
    internal sealed class KeycloakAdmin : IDisposable
    {
        // The bootstrap admin .github/scripts/keycloak.sh starts the container with.
        private const string AdminUsername = "admin";
        private const string AdminPassword = "admin";

        private static readonly string? ConfiguredUrl = Environment.GetEnvironmentVariable("KEYCLOAK_URL");

        private readonly HttpClient _client;

        private KeycloakAdmin(HttpClient client, string realm)
        {
            _client = client;
            Realm = realm;
        }

        /// <summary>
        /// Whether <c>KEYCLOAK_URL</c> names a Keycloak to run against.
        /// </summary>
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(ConfiguredUrl);

        /// <summary>
        /// The Keycloak's address, ending in a slash.
        /// </summary>
        public static Uri BaseUrl => string.IsNullOrWhiteSpace(ConfiguredUrl)
            ? throw new InvalidOperationException("KEYCLOAK_URL is not set.")
            : new Uri(ConfiguredUrl.TrimEnd('/') + "/");

        /// <summary>
        /// The realm's name.
        /// </summary>
        public string Realm { get; }

        /// <summary>
        /// The realm's address, which is its issuer and its SAML entity id.
        /// </summary>
        public string RealmUrl => new Uri(BaseUrl, $"realms/{Realm}").ToString();

        /// <summary>
        /// Creates a realm named <paramref name="namePrefix"/> and a random suffix, with one enabled user,
        /// <paramref name="username"/> with <paramref name="password"/>, whose email is the username at
        /// <c>example.test</c>, verified, and whose name is <paramref name="firstName"/> User.
        /// </summary>
        public static async Task<KeycloakAdmin> CreateRealmAsync(string namePrefix, string username, string password, string firstName)
        {
            KeycloakAdmin admin = new KeycloakAdmin(await SignInAsync(), namePrefix + Guid.NewGuid().ToString("N")[..12]);
            try
            {
                await admin.PostAsync("admin/realms", new { realm = admin.Realm, enabled = true });
                await admin.PostToRealmAsync("users", new
                {
                    username,
                    enabled = true,
                    email = EmailOf(username),
                    emailVerified = true,
                    firstName,
                    lastName = "User",
                    credentials = new[] { new { type = "password", value = password, temporary = false } },
                });
                return admin;
            }
            catch
            {
                admin.Dispose();
                throw;
            }
        }

        /// <summary>
        /// The email <see cref="CreateRealmAsync"/> gives a user.
        /// </summary>
        public static string EmailOf(string username) => $"{username}@example.test";

        /// <summary>
        /// Posts <paramref name="body"/> as JSON to <paramref name="path"/> under the realm's admin API, failing the test
        /// when Keycloak refuses it.
        /// </summary>
        public async Task PostToRealmAsync(string path, object body)
        {
            await PostAsync($"admin/realms/{Realm}/{path}", body);
        }

        /// <summary>
        /// Gets <paramref name="path"/> under the realm's admin API as JSON, failing the test when Keycloak refuses it.
        /// </summary>
        public async Task<JsonDocument> GetFromRealmAsync(string path)
        {
            HttpResponseMessage response = await _client.GetAsync($"admin/realms/{Realm}/{path}");
            await EnsureSuccessAsync(response, path);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        /// <summary>
        /// Gets one of the realm's public documents, at <paramref name="path"/> under <see cref="RealmUrl"/>, as text.
        /// </summary>
        public async Task<string> GetRealmDocumentAsync(string path)
        {
            HttpResponseMessage response = await _client.GetAsync($"realms/{Realm}/{path}");
            await EnsureSuccessAsync(response, path);
            return await response.Content.ReadAsStringAsync();
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private static async Task<HttpClient> SignInAsync()
        {
            HttpClient client = new HttpClient { BaseAddress = BaseUrl };
            try
            {
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
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private async Task PostAsync(string path, object body)
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(path, body);
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
