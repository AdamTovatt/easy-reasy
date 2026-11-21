using System.Net;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// How the token endpoint authenticates the calling client: only by the method it registered, with every malformed or
    /// mixed attempt refused as <c>invalid_client</c> and a Basic attempt answered with a Basic challenge.
    /// </summary>
    [TestClass]
    public class OAuthClientAuthenticationTests
    {
        private const string RedirectUri = "https://client.example/callback";

        [TestMethod]
        public async Task Token_WrongClientSecret_Is401InvalidClientWithABasicChallenge()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri, OAuthClientAuthenticationMethod.ClientSecretBasic);
            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());

            using HttpResponseMessage response = await RefreshAsync(server, tokens, Basic(client.ClientId, "wrong"));

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_client");
            StringAssert.StartsWith(response.Headers.WwwAuthenticate.ToString(), "Basic");
            Assert.AreEqual(OAuthTokenFailureReason.InvalidClient, server.AuditLogger.Tokens.Last().FailureReason);
            Assert.IsNull(server.Store.RefreshTokens.Single().ConsumedAt);
        }

        [TestMethod]
        public async Task Token_ConfidentialClientSendingNoSecret_Is401InvalidClientWithoutAChallenge()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri, OAuthClientAuthenticationMethod.ClientSecretPost);
            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(client with { AuthenticationMethod = OAuthClientAuthenticationMethod.None }, tokens.RefreshToken));

            Assert.AreEqual(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
        }

        [TestMethod]
        public async Task Token_SecretSentByTheUnregisteredMethod_Is401InvalidClient()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri, OAuthClientAuthenticationMethod.ClientSecretPost);
            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.RefreshAsync(client with { AuthenticationMethod = OAuthClientAuthenticationMethod.ClientSecretBasic }, tokens.RefreshToken));

            Assert.AreEqual(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.AreEqual("invalid_client", refused.Error);
        }

        [TestMethod]
        [DataRow("Basic not-base64!")]
        [DataRow("Basic Y2xpZW50LW9ubHk=")]
        public async Task Token_MalformedBasicHeader_Is401InvalidClientWithABasicChallenge(string authorization)
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri, OAuthClientAuthenticationMethod.ClientSecretBasic);
            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());

            using HttpResponseMessage response = await RefreshAsync(server, tokens, authorization);

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_client");
            StringAssert.StartsWith(response.Headers.WwwAuthenticate.ToString(), "Basic");
        }

        [TestMethod]
        public async Task Token_BasicHeaderAndFormSecret_Is401InvalidClient()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri, OAuthClientAuthenticationMethod.ClientSecretBasic);
            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());

            using HttpResponseMessage response = await RefreshAsync(server, tokens, Basic(client.ClientId, client.ClientSecret!), ("client_secret", client.ClientSecret!));

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_client");
        }

        [TestMethod]
        public async Task Token_FormClientIdOtherThanTheBasicHeaders_Is401InvalidClient()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync(RedirectUri, OAuthClientAuthenticationMethod.ClientSecretBasic);
            OAuthTestRegisteredClient other = await server.OAuth.RegisterAsync(RedirectUri);
            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());

            using HttpResponseMessage response = await RefreshAsync(server, tokens, Basic(client.ClientId, client.ClientSecret!), ("client_id", other.ClientId));

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_client");
            Assert.AreEqual(other.ClientId, server.AuditLogger.Tokens.Last().ClientId);
        }

        [TestMethod]
        public async Task Token_NoClientNamed_Is401InvalidClientWithoutAChallenge()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await RefreshAsync(server, connected.Tokens, authorization: null);

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_client");
            Assert.AreEqual(0, response.Headers.WwwAuthenticate.Count);
        }

        [TestMethod]
        public async Task Token_PublicClientSendingASecret_Is401InvalidClient()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            using HttpResponseMessage response = await RefreshAsync(server, connected.Tokens, authorization: null, ("client_id", connected.Client.ClientId), ("client_secret", "anything"));

            await OAuthTokenRefusalTests.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "invalid_client");
        }

        private static string Basic(string clientId, string secret)
        {
            return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.EscapeDataString(clientId) + ":" + Uri.EscapeDataString(secret)));
        }

        private static Task<HttpResponseMessage> RefreshAsync(HostedOAuthServer server, OAuthTestTokens tokens, string? authorization, params (string Name, string Value)[] extraFields)
        {
            List<KeyValuePair<string, string>> fields =
            [
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", tokens.RefreshToken),
            ];
            fields.AddRange(extraFields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));
            return server.PostFormAsync(OAuthServerPaths.Token, fields, authorization);
        }
    }
}
