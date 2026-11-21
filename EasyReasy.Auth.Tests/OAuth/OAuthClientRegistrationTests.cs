using System.Net;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Dynamic client registration (RFC 7591): what is recorded, what is refused, and that registering grants nothing.
    /// </summary>
    [TestClass]
    public class OAuthClientRegistrationTests
    {
        [TestMethod]
        public async Task Register_PublicClient_IsRecordedWithoutASecret()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            (HttpStatusCode status, JsonElement body) = await RegisterAsync(server,
                "{\"redirect_uris\":[\"https://claude.ai/api/mcp/auth_callback\"],\"token_endpoint_auth_method\":\"none\",\"client_name\":\"Claude\",\"grant_types\":[\"authorization_code\",\"refresh_token\"],\"response_types\":[\"code\"],\"scope\":\"anything\"}");

            Assert.AreEqual(HttpStatusCode.Created, status);
            Assert.IsFalse(body.TryGetProperty("client_secret", out _));
            Assert.AreEqual("none", body.GetProperty("token_endpoint_auth_method").GetString());
            Assert.AreEqual("Claude", body.GetProperty("client_name").GetString());
            OAuthClient stored = server.Store.Clients.Single();
            Assert.AreEqual(body.GetProperty("client_id").GetString(), stored.ClientId);
            Assert.AreEqual(OAuthClientAuthenticationMethod.None, stored.AuthenticationMethod);
            Assert.IsNull(stored.ClientSecretHash);
            Assert.AreEqual("Claude", server.AuditLogger.Registrations.Single().ClientName);
        }

        [TestMethod]
        public async Task Register_NamingNoMethod_IsConfidentialWithAHashedSecret()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            (HttpStatusCode status, JsonElement body) = await RegisterAsync(server, "{\"redirect_uris\":[\"https://client.example/cb\"]}");

            Assert.AreEqual(HttpStatusCode.Created, status);
            string secret = body.GetProperty("client_secret").GetString()!;
            Assert.AreEqual("client_secret_basic", body.GetProperty("token_endpoint_auth_method").GetString());
            Assert.AreEqual(0, body.GetProperty("client_secret_expires_at").GetInt64());
            OAuthClient stored = server.Store.Clients.Single();
            Assert.AreEqual(OAuthClientAuthenticationMethod.ClientSecretBasic, stored.AuthenticationMethod);
            Assert.AreEqual(SecureToken.Hash(secret), stored.ClientSecretHash);
        }

        [TestMethod]
        public async Task Register_GrantsNothing()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            await server.OAuth.RegisterAsync("https://client.example/cb");

            Assert.AreEqual(0, server.Store.AuthorizationCodes.Count);
            Assert.AreEqual(0, server.Store.RefreshTokens.Count);
            Assert.AreEqual(0, server.Handler.ApprovalCalls.Count);
        }

        [TestMethod]
        [DataRow("{\"client_name\":\"x\"}", "invalid_redirect_uri", DisplayName = "no redirect_uris")]
        [DataRow("{\"redirect_uris\":[]}", "invalid_redirect_uri", DisplayName = "empty redirect_uris")]
        [DataRow("{\"redirect_uris\":[\"http://client.example/cb\"]}", "invalid_redirect_uri", DisplayName = "http off loopback")]
        [DataRow("{\"redirect_uris\":[\"https://client.example/cb#fragment\"]}", "invalid_redirect_uri", DisplayName = "fragment")]
        [DataRow("{\"redirect_uris\":[\"javascript:alert(1)\"]}", "invalid_redirect_uri", DisplayName = "javascript scheme")]
        [DataRow("{\"redirect_uris\":[\"data:text/html,hi\"]}", "invalid_redirect_uri", DisplayName = "data scheme")]
        [DataRow("{\"redirect_uris\":[\"file:///etc/passwd\"]}", "invalid_redirect_uri", DisplayName = "file scheme")]
        [DataRow("{\"redirect_uris\":[\"https://user:pass@client.example/cb\"]}", "invalid_redirect_uri", DisplayName = "userinfo")]
        [DataRow("{\"redirect_uris\":[\"/relative\"]}", "invalid_redirect_uri", DisplayName = "relative")]
        [DataRow("{\"redirect_uris\":[\"https://client.example/cb\"],\"token_endpoint_auth_method\":\"private_key_jwt\"}", "invalid_client_metadata", DisplayName = "unsupported method")]
        [DataRow("{\"redirect_uris\":[\"https://client.example/cb\"],\"grant_types\":[\"client_credentials\"]}", "invalid_client_metadata", DisplayName = "no authorization_code grant")]
        [DataRow("{\"redirect_uris\":[\"https://client.example/cb\"],\"response_types\":[\"token\"]}", "invalid_client_metadata", DisplayName = "no code response type")]
        [DataRow("{\"redirect_uris\":[\"https://client.example/cb\"],\"client_uri\":\"javascript:x\"}", "invalid_client_metadata", DisplayName = "client_uri not https")]
        [DataRow("{\"redirect_uris\":[\"https://client.example/cb\"],\"client_name\":5}", "invalid_client_metadata", DisplayName = "client_name not a string")]
        [DataRow("[1,2]", "invalid_client_metadata", DisplayName = "not an object")]
        [DataRow("not json", "invalid_client_metadata", DisplayName = "not json")]
        public async Task Register_InvalidMetadata_IsRefusedWithItsError(string body, string expectedError)
        {
            await AssertRefusedAsync(body, expectedError);
        }

        [TestMethod]
        public async Task Register_NameOverTheLimit_IsRefused()
        {
            await AssertRefusedAsync(
                "{\"redirect_uris\":[\"https://client.example/cb\"],\"client_name\":\"" + new string('a', OAuthClientMetadata.MaxClientNameLength + 1) + "\"}",
                "invalid_client_metadata");
        }

        [TestMethod]
        public async Task Register_MoreRedirectUrisThanTheLimit_IsRefused()
        {
            string uris = string.Join(",", Enumerable.Range(0, OAuthClientMetadata.MaxRedirectUris + 1).Select(index => $"\"https://client.example/cb{index}\""));

            await AssertRefusedAsync("{\"redirect_uris\":[" + uris + "]}", "invalid_redirect_uri");
        }

        [TestMethod]
        public async Task Register_BodyOverTheLimit_IsRefused()
        {
            await AssertRefusedAsync(
                "{\"redirect_uris\":[\"https://client.example/cb\"],\"padding\":\"" + new string('a', OAuthJson.MaxRequestBodyBytes) + "\"}",
                "invalid_client_metadata");
        }

        private static async Task AssertRefusedAsync(string body, string expectedError)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            (HttpStatusCode status, JsonElement response) = await RegisterAsync(server, body);

            Assert.AreEqual(HttpStatusCode.BadRequest, status);
            Assert.AreEqual(expectedError, response.GetProperty("error").GetString());
            Assert.AreEqual(0, server.Store.Clients.Count);
            Assert.AreEqual(expectedError, server.AuditLogger.Registrations.Single().FailureCode);
        }

        [TestMethod]
        [DataRow("http://127.0.0.1:8080/callback")]
        [DataRow("http://localhost/callback")]
        [DataRow("http://[::1]/callback")]
        [DataRow("com.example.app:/oauth2redirect")]
        [DataRow("cursor://anysphere.cursor-retrieval/oauth/callback")]
        public async Task Register_NativeAppRedirect_IsAccepted(string redirectUri)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            (HttpStatusCode status, _) = await RegisterAsync(server, "{\"redirect_uris\":[" + JsonSerializer.Serialize(redirectUri) + "],\"token_endpoint_auth_method\":\"none\"}");

            Assert.AreEqual(HttpStatusCode.Created, status);
        }

        private static async Task<(HttpStatusCode Status, JsonElement Body)> RegisterAsync(HostedOAuthServer server, string body)
        {
            using HttpResponseMessage response = await server.Client.PostAsync(OAuthServerPaths.Register, new StringContent(body, Encoding.UTF8, "application/json"));
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return (response.StatusCode, document.RootElement.Clone());
        }
    }
}
