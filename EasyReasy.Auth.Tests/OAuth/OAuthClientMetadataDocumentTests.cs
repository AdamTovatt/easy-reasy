using System.Net;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A client identified by the https URL of its client ID metadata document: the server fetches the document at the
    /// authorization endpoint, checks it, and never stores the client.
    /// </summary>
    [TestClass]
    public class OAuthClientMetadataDocumentTests
    {
        private const string ClientId = "https://agent.example/oauth/client.json";
        private const string RedirectUri = "https://agent.example/callback";

        [TestMethod]
        public async Task Connect_MetadataDocumentClient_Works()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, Document());

            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(Client(), HostedOAuthServer.Resource);
            OAuthAuthorizationRequestDetails details = await server.OAuth.GetRequestAsync(authorization, server.SignIn());
            string code = await server.OAuth.ApproveAsync(authorization, server.SignIn());
            OAuthTestTokens tokens = await server.OAuth.RedeemCodeAsync(authorization, code);
            OAuthTestTokens refreshed = await server.OAuth.RefreshAsync(Client(), tokens.RefreshToken);

            Assert.AreEqual(OAuthClientRegistrationKind.MetadataDocument, details.Client.RegistrationKind);
            Assert.AreEqual("agent.example", details.Client.MetadataDocumentHost);
            Assert.AreEqual("Agent", details.Client.ClientName);
            Assert.AreEqual(HttpStatusCode.OK, (await server.GetMeAsync(refreshed.AccessToken)).StatusCode);
            Assert.AreEqual(0, server.Store.Clients.Count);
            HttpRequestMessage fetch = server.MetadataDocuments.Requests.Single();
            Assert.AreEqual("application/json", fetch.Headers.Accept.Single().MediaType);
        }

        [TestMethod]
        public async Task GetRequest_InternationalizedHost_ShowsItInPunycode()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            const string lookAlikeClientId = "https://\u0430pple.example/client.json";
            server.MetadataDocuments.Serve(lookAlikeClientId, "{\"client_id\":\"" + lookAlikeClientId + "\",\"redirect_uris\":[\"" + RedirectUri + "\"]}");

            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(Client() with { ClientId = lookAlikeClientId });
            OAuthAuthorizationRequestDetails details = await server.OAuth.GetRequestAsync(authorization, server.SignIn());

            Assert.AreEqual("xn--pple-43d.example", details.Client.MetadataDocumentHost);
        }

        [TestMethod]
        public async Task Authorize_SameClientAgain_FetchesTheDocumentOnce()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, Document());

            await server.OAuth.AuthorizeAsync(Client());
            server.Clock.Advance(OAuthClientMetadataDocumentFetcher.CacheLifetime - TimeSpan.FromSeconds(1));
            await server.OAuth.AuthorizeAsync(Client());

            Assert.AreEqual(1, server.MetadataDocuments.Requests.Count);
        }

        [TestMethod]
        public async Task Authorize_AfterTheCacheLifetime_FetchesTheDocumentAgain()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, Document());

            await server.OAuth.AuthorizeAsync(Client());
            server.Clock.Advance(OAuthClientMetadataDocumentFetcher.CacheLifetime);
            await server.OAuth.AuthorizeAsync(Client());

            Assert.AreEqual(2, server.MetadataDocuments.Requests.Count);
        }

        [TestMethod]
        public async Task Authorize_AfterAnUnusableDocument_FetchesTheFixedOne()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, "not json");
            await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.AuthorizeAsync(Client()));
            server.MetadataDocuments.Serve(ClientId, Document());

            await server.OAuth.AuthorizeAsync(Client());

            Assert.AreEqual(1, server.Store.AuthorizationRequests.Count);
            Assert.AreEqual(2, server.MetadataDocuments.Requests.Count);
        }

        [TestMethod]
        [DataRow("{\"client_id\":\"https://agent.example/other.json\",\"redirect_uris\":[\"https://agent.example/callback\"]}", DisplayName = "client_id is another URL")]
        [DataRow("{\"client_id\":\"https://agent.example/oauth/client.json/\",\"redirect_uris\":[\"https://agent.example/callback\"]}", DisplayName = "client_id differs by a slash")]
        [DataRow("{\"client_id\":\"https://agent.example/oauth/client.json\",\"redirect_uris\":[\"https://agent.example/callback\"],\"client_secret\":\"s\"}", DisplayName = "carries a secret")]
        [DataRow("{\"client_id\":\"https://agent.example/oauth/client.json\",\"redirect_uris\":[\"https://agent.example/callback\"],\"token_endpoint_auth_method\":\"private_key_jwt\"}", DisplayName = "private_key_jwt")]
        [DataRow("{\"client_id\":\"https://agent.example/oauth/client.json\",\"redirect_uris\":[\"https://agent.example/callback\"],\"token_endpoint_auth_method\":\"client_secret_basic\"}", DisplayName = "secret method")]
        [DataRow("{\"client_id\":\"https://agent.example/oauth/client.json\"}", DisplayName = "no redirect_uris")]
        [DataRow("not json", DisplayName = "not json")]
        public async Task Authorize_UnusableDocument_Is400AndDoesNotRedirect(string document)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, document);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.AuthorizeAsync(Client()));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
        }

        [TestMethod]
        public async Task Authorize_RedirectUriNotInTheDocument_Is400AndDoesNotRedirect()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, "{\"client_id\":\"" + ClientId + "\",\"redirect_uris\":[\"https://agent.example/elsewhere\"]}");

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.AuthorizeAsync(Client()));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_request", refused.Error);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.NotFound)]
        [DataRow(HttpStatusCode.Found)]
        [DataRow(HttpStatusCode.InternalServerError)]
        public async Task Authorize_DocumentNotAnswered200_Is400(HttpStatusCode status)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, Document(), status);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.AuthorizeAsync(Client()));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
            StringAssert.Contains(refused.ErrorDescription, "answered " + (int)status);
        }

        [TestMethod]
        public async Task Authorize_DocumentOverTheSizeLimit_Is400()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, Document().TrimEnd('}') + ",\"padding\":\"" + new string('a', OAuthClientMetadataDocumentFetcher.MaxDocumentBytes) + "\"}");

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.AuthorizeAsync(Client()));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
            StringAssert.Contains(refused.ErrorDescription, "at most " + OAuthClientMetadataDocumentFetcher.MaxDocumentBytes + " bytes");
        }

        [TestMethod]
        public async Task Authorize_DocumentsOff_IsRefusedWithoutFetching()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(HostedOAuthServer.DefaultOptions() with { AllowClientIdMetadataDocuments = false });
            server.MetadataDocuments.Serve(ClientId, Document());

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.AuthorizeAsync(Client()));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
            Assert.AreEqual(0, server.MetadataDocuments.Requests.Count);
        }

        [TestMethod]
        public async Task Authorize_DocumentOnALoopbackHost_IsRefusedByTheAddressGuard()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync(realMetadataDocumentFetch: true);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.AuthorizeAsync(Client() with { ClientId = "https://localhost:1/client.json" }));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
            StringAssert.Contains(refused.ErrorDescription, "does not resolve to a public address");
        }

        [TestMethod]
        public async Task Token_MetadataDocumentClientSendingASecret_Is401()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.MetadataDocuments.Serve(ClientId, Document());
            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(Client(), server.SignIn());
            OAuthTestRegisteredClient withSecret = Client() with { ClientSecret = "made-up", AuthenticationMethod = OAuthClientAuthenticationMethod.ClientSecretPost };

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.RefreshAsync(withSecret, tokens.RefreshToken));

            Assert.AreEqual(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
        }

        [TestMethod]
        [DataRow("https://agent.example/client.json", true)]
        [DataRow("https://agent.example:8443/client.json?v=1", true)]
        [DataRow("http://agent.example/client.json", false)]
        [DataRow("https://agent.example/", false)]
        [DataRow("https://agent.example", false)]
        [DataRow("https://user@agent.example/client.json", false)]
        [DataRow("https://agent.example/a/../client.json", false)]
        [DataRow("https://agent.example/./client.json", false)]
        [DataRow("https://agent.example/%2e%2e/client.json", false)]
        [DataRow("https://agent.example/client.json#x", false)]
        public void IsValid_AppliesTheDraftsRules(string clientId, bool allowed)
        {
            Assert.AreEqual(allowed, OAuthClientIdUrl.IsValid(clientId));
        }

        private static OAuthTestRegisteredClient Client()
        {
            return new OAuthTestRegisteredClient
            {
                ClientId = ClientId,
                AuthenticationMethod = OAuthClientAuthenticationMethod.None,
                RedirectUri = RedirectUri,
            };
        }

        private static string Document()
        {
            return "{\"client_id\":\"" + ClientId + "\",\"client_name\":\"Agent\",\"client_uri\":\"https://agent.example\",\"redirect_uris\":[\"" + RedirectUri + "\"],\"grant_types\":[\"authorization_code\",\"refresh_token\"],\"response_types\":[\"code\"],\"token_endpoint_auth_method\":\"none\"}";
        }
    }
}
