using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The SAML realms the integration tests sign in against, each created through <see cref="KeycloakAdmin"/> with one
    /// user and one SAML client for the test service provider.
    /// </summary>
    internal static class KeycloakServer
    {
        public const string Username = "saml-user";
        public const string Password = "saml-user-password";

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
            using KeycloakAdmin admin = await KeycloakAdmin.CreateRealmAsync("saml-", Username, Password, "Saml");

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

            await admin.PostToRealmAsync("clients", new
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

            string metadataXml = await admin.GetRealmDocumentAsync("protocol/saml/descriptor");
            return new KeycloakRealm(admin.Realm, admin.RealmUrl, metadataXml);
        }
    }
}
