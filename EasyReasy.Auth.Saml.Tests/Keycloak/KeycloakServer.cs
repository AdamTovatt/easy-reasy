using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text.Json;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The Keycloak the integration tests sign in against, at <c>KEYCLOAK_URL</c>, with the bootstrap admin the start
    /// script creates. Each test class sets up realms of its own through the admin API, named uniquely, so runs never see
    /// each other's state and nothing needs cleaning between them.
    /// </summary>
    /// <remarks>
    /// Started by <c>.github/scripts/keycloak.sh</c>, which sets <c>KEYCLOAK_URL</c> in CI and prints it otherwise;
    /// CLAUDE.md has the local commands.
    /// </remarks>
    internal static class KeycloakServer
    {
        public const string Username = "saml-user";
        public const string Password = "saml-user-password";

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
        /// Creates a realm with one user and one SAML client for the test service provider: it signs the response and the
        /// assertion with <paramref name="signatureAlgorithm"/> (Keycloak's name for it) and exclusive canonicalization,
        /// requires AuthnRequests signed by <paramref name="requestSigningCertificate"/>, sends a persistent NameID and the
        /// username as the <c>uid</c> attribute, and, when <paramref name="encryptionCertificate"/> is given, encrypts the
        /// assertion for it with AES-256-GCM and XML Encryption 1.1's RSA-OAEP over SHA-256.
        /// </summary>
        public static async Task<KeycloakRealm> CreateRealmAsync(
            X509Certificate2 requestSigningCertificate,
            X509Certificate2? encryptionCertificate = null,
            string signatureAlgorithm = "RSA_SHA256")
        {
            string realm = "saml-" + Guid.NewGuid().ToString("N")[..12];
            using HttpClient admin = await CreateAdminClientAsync();

            await PostAsync(admin, "admin/realms", new { realm, enabled = true });
            await PostAsync(admin, $"admin/realms/{realm}/users", new
            {
                username = Username,
                enabled = true,
                email = "saml-user@example.test",
                emailVerified = true,
                firstName = "Saml",
                lastName = "User",
                credentials = new[] { new { type = "password", value = Password, temporary = false } },
            });

            Dictionary<string, string> attributes = new Dictionary<string, string>
            {
                ["saml_assertion_consumer_url_post"] = SamlValidationHarness.AssertionConsumerServiceUrl,
                ["saml.server.signature"] = "true",
                ["saml.assertion.signature"] = "true",
                ["saml.signature.algorithm"] = signatureAlgorithm,
                ["saml_signature_canonicalization_method"] = "http://www.w3.org/2001/10/xml-exc-c14n#",
                ["saml.client.signature"] = "true",
                ["saml.signing.certificate"] = Convert.ToBase64String(requestSigningCertificate.RawData),
                ["saml_force_name_id_format"] = "true",
                ["saml_name_id_format"] = "persistent",
                ["saml.authnstatement"] = "true",
                ["saml.encrypt"] = encryptionCertificate == null ? "false" : "true",
            };

            if (encryptionCertificate != null)
            {
                attributes["saml.encryption.certificate"] = Convert.ToBase64String(encryptionCertificate.RawData);
                attributes["saml.encryption.algorithm"] = SamlAlgorithms.Aes256Gcm;
                attributes["saml.encryption.keyAlgorithm"] = SamlAlgorithms.RsaOaep11;
                attributes["saml.encryption.digestMethod"] = EncryptedXml.XmlEncSHA256Url;
            }

            await PostAsync(admin, $"admin/realms/{realm}/clients", new
            {
                clientId = SamlValidationHarness.ServiceProviderEntityId,
                protocol = "saml",
                enabled = true,
                redirectUris = new[] { SamlValidationHarness.AssertionConsumerServiceUrl },
                attributes,
                protocolMappers = new[]
                {
                    new
                    {
                        name = "uid",
                        protocol = "saml",
                        protocolMapper = "saml-user-property-mapper",
                        config = new Dictionary<string, string>
                        {
                            ["user.attribute"] = "username",
                            ["attribute.name"] = "uid",
                            ["attribute.nameformat"] = "Basic",
                        },
                    },
                },
            });

            using HttpClient anonymous = new HttpClient { BaseAddress = BaseUrl };
            string metadataXml = await anonymous.GetStringAsync($"realms/{realm}/protocol/saml/descriptor");
            return new KeycloakRealm(realm, new Uri(BaseUrl, $"realms/{realm}").ToString(), metadataXml);
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
