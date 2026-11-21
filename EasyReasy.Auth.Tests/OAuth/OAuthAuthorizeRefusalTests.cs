using System.Net;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the authorization endpoint refuses, and how: before the client and redirect URI are known good it answers on
    /// the page and never redirects; after, it redirects the error to the client.
    /// </summary>
    [TestClass]
    public class OAuthAuthorizeRefusalTests
    {
        private const string RedirectUri = "https://client.example/callback";

        [TestMethod]
        public async Task Authorize_UnregisteredRedirectUri_Is400AndDoesNotRedirect()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.AuthorizeAsync(client, redirectUri: "https://attacker.example/callback"));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_request", refused.Error);
            Assert.AreEqual(0, server.Store.AuthorizationRequests.Count);
        }

        [TestMethod]
        public async Task Authorize_RedirectUriDifferingOnlyInPath_IsRefused()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.AuthorizeAsync(client, redirectUri: RedirectUri + "/../evil"));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_request", refused.Error);
            Assert.AreEqual(0, server.Store.AuthorizationRequests.Count);
        }

        [TestMethod]
        public async Task Authorize_HttpsRedirectOnAnotherPort_IsRefused()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.AuthorizeAsync(client, redirectUri: "https://client.example:8443/callback"));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_request", refused.Error);
            Assert.AreEqual(0, server.Store.AuthorizationRequests.Count);
        }

        [TestMethod]
        public async Task Authorize_UnknownClient_Is400AndDoesNotRedirect()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient unknown = new OAuthTestRegisteredClient
            {
                ClientId = "never-registered",
                AuthenticationMethod = OAuthClientAuthenticationMethod.None,
                RedirectUri = RedirectUri,
            };

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.AuthorizeAsync(unknown));

            Assert.AreEqual(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
        }

        [TestMethod]
        public async Task Authorize_WithoutCodeChallenge_RedirectsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            Dictionary<string, string> error = await AuthorizeExpectingClientRedirectAsync(server, client, codeChallenge: null, codeChallengeMethod: null);

            Assert.AreEqual("invalid_request", error["error"]);
            Assert.AreEqual("state-1", error["state"]);
            Assert.AreEqual(HostedOAuthServer.Issuer, error["iss"]);
        }

        [TestMethod]
        public async Task Authorize_PlainCodeChallenge_RedirectsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            string verifier = OAuthTestPkce.CreateVerifier();

            Dictionary<string, string> error = await AuthorizeExpectingClientRedirectAsync(server, client, codeChallenge: verifier, codeChallengeMethod: "plain");

            Assert.AreEqual("invalid_request", error["error"]);
        }

        [TestMethod]
        public async Task Authorize_ChallengeWithoutMethod_IsPlainAndRedirectsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            Dictionary<string, string> error = await AuthorizeExpectingClientRedirectAsync(
                server, client, codeChallenge: OAuthTestPkce.ChallengeFor(OAuthTestPkce.CreateVerifier()), codeChallengeMethod: null);

            Assert.AreEqual("invalid_request", error["error"]);
        }

        [TestMethod]
        public async Task Authorize_ImplicitResponseType_RedirectsUnsupportedResponseType()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            Dictionary<string, string> error = await AuthorizeExpectingClientRedirectAsync(server, client, responseType: "token");

            Assert.AreEqual("unsupported_response_type", error["error"]);
        }

        [TestMethod]
        public async Task Authorize_ResourceOfAnotherServer_RedirectsInvalidTarget()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.AuthorizeAsync(client, resource: "https://other.example/mcp"));

            Assert.AreEqual("invalid_target", refused.Error);
        }

        [TestMethod]
        public async Task Authorize_ResourceUnderTheConfiguredOne_IsAccepted()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            await server.OAuth.AuthorizeAsync(client, resource: "HTTPS://APP.EXAMPLE/mcp/tools/");

            Assert.AreEqual("HTTPS://APP.EXAMPLE/mcp/tools/", server.Store.AuthorizationRequests.Single().Resource);
        }

        [TestMethod]
        public async Task Authorize_ResourceBesideTheConfiguredOne_IsRefused()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.AuthorizeAsync(client, resource: "https://app.example/mcpx"));

            Assert.AreEqual("invalid_target", refused.Error);
        }

        [TestMethod]
        public async Task Authorize_RepeatedParameter_Is400()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            using HttpResponseMessage response = await server.Client.GetAsync(
                OAuthServerPaths.Authorize + "?client_id=" + client.ClientId + "&client_id=" + client.ClientId + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri));

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.IsNull(response.Headers.Location);
        }

        [TestMethod]
        public async Task Authorize_NoClientId_Is400AndDoesNotRedirect()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            using HttpResponseMessage response = await server.Client.GetAsync(OAuthServerPaths.Authorize + "?response_type=code&redirect_uri=" + Uri.EscapeDataString(RedirectUri));

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
            Assert.IsNull(response.Headers.Location);
        }

        [TestMethod]
        public async Task Authorize_NoRedirectUri_Is400AndDoesNotRedirect()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            using HttpResponseMessage response = await server.Client.GetAsync(OAuthServerPaths.Authorize + "?response_type=code&client_id=" + client.ClientId);

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.BadRequest, "invalid_request");
            Assert.IsNull(response.Headers.Location);
        }

        [TestMethod]
        public async Task Authorize_MalformedChallengeOfTheRightLength_RedirectsInvalidRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            Dictionary<string, string> error = await AuthorizeExpectingClientRedirectAsync(server, client, codeChallenge: new string('*', OAuthPkce.ChallengeLength));

            Assert.AreEqual("invalid_request", error["error"]);
            Assert.AreEqual(0, server.Store.AuthorizationRequests.Count);
        }

        [TestMethod]
        [DataRow("https://app.example/mcp?tenant=a")]
        [DataRow("https://app.example/mcp#part")]
        public async Task Authorize_ResourceWithQueryOrFragment_RedirectsInvalidTarget(string resource)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(() => server.OAuth.AuthorizeAsync(client, resource: resource));

            Assert.AreEqual("invalid_target", refused.Error);
            Assert.AreEqual(0, server.Store.AuthorizationRequests.Count);
        }

        [TestMethod]
        [DataRow("state")]
        [DataRow("scope")]
        [DataRow("resource")]
        public async Task Authorize_StoredParameterOverTheLimit_RedirectsInvalidRequestWithoutState(string parameter)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri);
            string oversized = parameter == "resource"
                ? HostedOAuthServer.Resource + "/" + new string('a', OAuthAuthorizationEndpoint.MaxStoredParameterLength)
                : new string('a', OAuthAuthorizationEndpoint.MaxStoredParameterLength + 1);

            Dictionary<string, string> error = await AuthorizeExpectingClientRedirectAsync(server, client, extra: (parameter, oversized));

            Assert.AreEqual("invalid_request", error["error"]);
            Assert.IsFalse(error.ContainsKey("state"));
            Assert.AreEqual(0, server.Store.AuthorizationRequests.Count);
        }

        private static async Task<Dictionary<string, string>> AuthorizeExpectingClientRedirectAsync(
            HostedOAuthServer server,
            OAuthTestRegisteredClient client,
            string? codeChallenge = "",
            string? codeChallengeMethod = "S256",
            string responseType = "code",
            (string Name, string Value)? extra = null)
        {
            Dictionary<string, string?> query = new Dictionary<string, string?>
            {
                ["response_type"] = responseType,
                ["client_id"] = client.ClientId,
                ["redirect_uri"] = client.RedirectUri,
                ["state"] = "state-1",
                ["code_challenge"] = codeChallenge == "" ? OAuthTestPkce.ChallengeFor(OAuthTestPkce.CreateVerifier()) : codeChallenge,
                ["code_challenge_method"] = codeChallengeMethod,
            };
            if (extra != null)
            {
                query[extra.Value.Name] = extra.Value.Value;
            }

            string url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(OAuthServerPaths.Authorize, query.Where(parameter => parameter.Value != null));
            using HttpResponseMessage response = await server.Client.GetAsync(url);

            Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
            string location = response.Headers.Location!.OriginalString;
            Assert.IsTrue(location.StartsWith(client.RedirectUri + "?", StringComparison.Ordinal), location);
            return OAuthTestClient.ParseQuery(location);
        }
    }
}
