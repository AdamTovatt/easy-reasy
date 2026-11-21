using System.Net;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What an MCP client reads first: the 401 pointing at the protected resource metadata, that document, and the
    /// authorization server metadata it names.
    /// </summary>
    [TestClass]
    public class OAuthDiscoveryTests
    {
        [TestMethod]
        public async Task ProtectedEndpoint_WithoutToken_401NamesTheResourceMetadata()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            using HttpResponseMessage response = await server.Client.GetAsync(HostedOAuthServer.MePath);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(
                "Bearer resource_metadata=\"https://app.example/.well-known/oauth-protected-resource/mcp\"",
                response.Headers.WwwAuthenticate.ToString());
        }

        [TestMethod]
        public async Task ProtectedEndpoint_WithBadToken_401KeepsTheErrorBesideTheResourceMetadata()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            using HttpResponseMessage response = await server.GetMeAsync("not-a-token");

            string challenge = response.Headers.WwwAuthenticate.ToString();
            StringAssert.StartsWith(challenge, "Bearer resource_metadata=\"https://app.example/.well-known/oauth-protected-resource/mcp\", error=\"invalid_token\"");
        }

        [TestMethod]
        public async Task ProtectedResourceMetadata_IsServedAtTheResourcesPath()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(WithScopes());

            JsonElement document = await GetJsonAsync(server, "/.well-known/oauth-protected-resource/mcp");

            Assert.AreEqual("https://app.example/mcp", document.GetProperty("resource").GetString());
            Assert.AreEqual("https://app.example", document.GetProperty("authorization_servers")[0].GetString());
            Assert.AreEqual("header", document.GetProperty("bearer_methods_supported")[0].GetString());
            Assert.AreEqual("files:read", document.GetProperty("scopes_supported")[0].GetString());
        }

        [TestMethod]
        public async Task ProtectedResourceMetadata_ForTheOrigin_IsServedAtTheRoot()
        {
            OAuthServerOptions options = new OAuthServerOptions { IssuerUrl = "https://app.example/", Resource = "https://app.example/", ConsentPageUrl = "/connect" };
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(options);

            JsonElement document = await GetJsonAsync(server, "/.well-known/oauth-protected-resource");

            Assert.AreEqual("https://app.example", document.GetProperty("resource").GetString());
        }

        [TestMethod]
        public async Task AuthorizationServerMetadata_ListsWhatAnMcpClientNeeds()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            JsonElement document = await GetJsonAsync(server, "/.well-known/oauth-authorization-server");

            Assert.AreEqual("https://app.example", document.GetProperty("issuer").GetString());
            Assert.AreEqual("https://app.example/oauth/authorize", document.GetProperty("authorization_endpoint").GetString());
            Assert.AreEqual("https://app.example/oauth/token", document.GetProperty("token_endpoint").GetString());
            Assert.AreEqual("https://app.example/oauth/register", document.GetProperty("registration_endpoint").GetString());
            Assert.AreEqual("https://app.example/oauth/revoke", document.GetProperty("revocation_endpoint").GetString());
            CollectionAssert.AreEqual(new[] { "S256" }, Strings(document, "code_challenge_methods_supported"));
            CollectionAssert.AreEqual(new[] { "code" }, Strings(document, "response_types_supported"));
            CollectionAssert.AreEqual(new[] { "authorization_code", "refresh_token" }, Strings(document, "grant_types_supported"));
            CollectionAssert.AreEqual(new[] { "none", "client_secret_basic", "client_secret_post" }, Strings(document, "token_endpoint_auth_methods_supported"));
            Assert.IsTrue(document.GetProperty("client_id_metadata_document_supported").GetBoolean());
            Assert.IsTrue(document.GetProperty("authorization_response_iss_parameter_supported").GetBoolean());
            Assert.IsFalse(document.TryGetProperty("scopes_supported", out _));
        }

        [TestMethod]
        public async Task AuthorizationServerMetadata_WithRegistrationAndDocumentsOff_SaysSo()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(
                HostedOAuthServer.DefaultOptions() with { AllowDynamicClientRegistration = false, AllowClientIdMetadataDocuments = false });

            JsonElement document = await GetJsonAsync(server, "/.well-known/oauth-authorization-server");
            using HttpResponseMessage register = await server.Client.PostAsync(OAuthServerPaths.Register, new StringContent("{\"redirect_uris\":[\"https://client.example/cb\"]}"));

            Assert.IsFalse(document.TryGetProperty("registration_endpoint", out _));
            Assert.IsFalse(document.GetProperty("client_id_metadata_document_supported").GetBoolean());
            Assert.AreEqual(HttpStatusCode.NotFound, register.StatusCode);
        }

        [TestMethod]
        public async Task Challenge_WithScopes_NamesThem()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(WithScopes());

            using HttpResponseMessage response = await server.Client.GetAsync(HostedOAuthServer.MePath);

            StringAssert.Contains(response.Headers.WwwAuthenticate.ToString(), "scope=\"files:read files:write\"");
        }

        private static OAuthServerOptions WithScopes()
        {
            return HostedOAuthServer.DefaultOptions() with { ScopesSupported = ["files:read", "files:write"] };
        }

        private static string[] Strings(JsonElement document, string property)
        {
            return document.GetProperty(property).EnumerateArray().Select(item => item.GetString()!).ToArray();
        }

        private static async Task<JsonElement> GetJsonAsync(HostedOAuthServer server, string path)
        {
            using HttpResponseMessage response = await server.Client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.Clone();
        }
    }
}
