using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The connection an MCP client makes, end to end: it registers, the person approves on the app's consent page, the
    /// client redeems the code, calls with the access token and refreshes it.
    /// </summary>
    [TestClass]
    public class OAuthServerFlowTests
    {
        [TestMethod]
        public async Task Connect_PublicClient_GetsTokensCarryingTheGrantAndTheClientId()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();

            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            Assert.AreEqual("Bearer", connected.Tokens.TokenType);
            Assert.AreEqual((long)HostedOAuthServer.DefaultOptions().AccessTokenLifetime.TotalSeconds, connected.Tokens.ExpiresIn);
            JsonElement me = await ReadMeAsync(server, connected.Tokens.AccessToken);
            Assert.AreEqual("person-1", me.GetProperty("sub").GetString());
            Assert.AreEqual(connected.Client.ClientId, me.GetProperty("client_id").GetString());
            Assert.AreEqual("org-7", me.GetProperty("organization_id").GetString());
            Assert.AreEqual("member", me.GetProperty("roles")[0].GetString());
            Assert.AreEqual(1, me.GetProperty("client_id_claims").GetInt32());
        }

        [TestMethod]
        public async Task Approve_HandlerSeesThePersonTheClientAndTheBody()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback", clientName: "Test Agent");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client, HostedOAuthServer.Resource, scope: "files:read files:write");

            await server.OAuth.ApproveAsync(authorization, server.SignIn("person-9"), new { organization = "org-3" });

            OAuthApprovalContext context = server.Handler.ApprovalCalls.Single();
            Assert.AreEqual("person-9", server.Handler.ApprovingUserIds.Single());
            Assert.AreEqual(client.ClientId, context.Client.ClientId);
            Assert.AreEqual("Test Agent", context.Client.ClientName);
            Assert.AreEqual(OAuthClientRegistrationKind.Registered, context.Client.RegistrationKind);
            Assert.AreEqual("https://client.example/callback", context.RedirectUri);
            CollectionAssert.AreEqual(new[] { "files:read", "files:write" }, context.RequestedScopes.ToArray());
            Assert.AreEqual(HostedOAuthServer.Resource, context.Resource);
            Assert.AreEqual("org-3", context.ApprovalBody!.Value.GetProperty("organization").GetString());
            Assert.AreEqual(authorization.RequestId, context.RequestId);
        }

        [TestMethod]
        public async Task Approve_GrantSubjectIsTheHandlers_NotTheApprovingPerson()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = context => OAuthApprovalDecision.Approve(new OAuthGrant
            {
                GrantId = "system-connection-1",
                Subject = "system-42",
                AuthType = "system",
            });

            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            JsonElement me = await ReadMeAsync(server, connected.Tokens.AccessToken);
            Assert.AreEqual("system-42", me.GetProperty("sub").GetString());
            Assert.AreEqual("system-connection-1", server.Store.RefreshTokens.Single().GrantId);
        }

        [TestMethod]
        public async Task GetRequest_ReturnsWhatTheConsentPageShows()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback", clientName: "Test Agent");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client, HostedOAuthServer.Resource, scope: "files:read");

            OAuthAuthorizationRequestDetails details = await server.OAuth.GetRequestAsync(authorization, server.SignIn());

            Assert.AreEqual(client.ClientId, details.Client.ClientId);
            Assert.AreEqual("Test Agent", details.Client.ClientName);
            Assert.IsNull(details.Client.MetadataDocumentHost);
            Assert.AreEqual("https://client.example/callback", details.RedirectUri);
            CollectionAssert.AreEqual(new[] { "files:read" }, details.Scopes.ToArray());
            Assert.AreEqual(HostedOAuthServer.Resource, details.Resource);
            Assert.IsTrue(authorization.ConsentPageUrl.StartsWith(HostedOAuthServer.ConsentPageUrl + "?request=", StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task Refresh_RotatesAndTheNewAccessTokenWorks()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();

            OAuthTestTokens refreshed = await server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken);

            Assert.AreNotEqual(connected.Tokens.RefreshToken, refreshed.RefreshToken);
            JsonElement me = await ReadMeAsync(server, refreshed.AccessToken);
            Assert.AreEqual("person-1", me.GetProperty("sub").GetString());
            Assert.AreEqual(connected.Client.ClientId, me.GetProperty("client_id").GetString());
            Assert.AreEqual("org-7", me.GetProperty("organization_id").GetString());
        }

        [TestMethod]
        public async Task Approve_GrantCarryingAClientIdClaim_IsDroppedForTheRealClient()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            server.Handler.Approve = context => OAuthApprovalDecision.Approve(new OAuthGrant
            {
                GrantId = "connection-5",
                Subject = "person-1",
                AuthType = "user",
                Claims = [new System.Security.Claims.Claim(EasyReasyClaimTypes.ClientId, "spoofed"), new System.Security.Claims.Claim("organization_id", "org-7")],
            });
            (OAuthTestRegisteredClient client, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync();

            OAuthTestTokens tokens = await server.OAuth.RedeemCodeAsync(authorization, code);

            Assert.IsFalse(RefreshTokenClaims.DeserializeClaims(server.Store.AuthorizationCodes.Single().SerializedClaims).Any(claim => claim.Type == EasyReasyClaimTypes.ClientId));
            JsonElement me = await ReadMeAsync(server, tokens.AccessToken);
            Assert.AreEqual(client.ClientId, me.GetProperty("client_id").GetString());
            Assert.AreEqual(1, me.GetProperty("client_id_claims").GetInt32());
            Assert.AreEqual("org-7", me.GetProperty("organization_id").GetString());
        }

        [TestMethod]
        public async Task Refresh_HandlerSeesTheGrantAndCanChangeTheClaims()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            HostedOAuthServer.ConnectedClient connected = await server.ConnectPublicClientAsync();
            server.Handler.Refresh = context => RefreshClaimsDecision.Allow(
                [new System.Security.Claims.Claim("organization_id", "org-8"), new System.Security.Claims.Claim(EasyReasyClaimTypes.ClientId, "spoofed")],
                ["viewer"]);

            OAuthTestTokens refreshed = await server.OAuth.RefreshAsync(connected.Client, connected.Tokens.RefreshToken);

            OAuthRefreshContext context = server.Handler.RefreshCalls.Single();
            Assert.AreEqual(server.Store.RefreshTokens.First().GrantId, context.GrantId);
            Assert.AreEqual(connected.Client.ClientId, context.ClientId);
            Assert.AreEqual("person-1", context.Subject);
            Assert.IsFalse(context.StoredClaims.Any(claim => claim.Type == EasyReasyClaimTypes.ClientId || claim.Type == EasyReasyClaimTypes.RefreshFamilyId));
            JsonElement me = await ReadMeAsync(server, refreshed.AccessToken);
            Assert.AreEqual("org-8", me.GetProperty("organization_id").GetString());
            Assert.AreEqual(connected.Client.ClientId, me.GetProperty("client_id").GetString());
            Assert.AreEqual(1, me.GetProperty("client_id_claims").GetInt32());
            Assert.AreEqual("viewer", me.GetProperty("roles")[0].GetString());
        }

        [TestMethod]
        public async Task Connect_ClientSecretBasic_Works()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback", OAuthClientAuthenticationMethod.ClientSecretBasic);

            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());
            OAuthTestTokens refreshed = await server.OAuth.RefreshAsync(client, tokens.RefreshToken);

            Assert.AreEqual(OAuthClientAuthenticationMethod.ClientSecretBasic, server.Store.Clients.Single().AuthenticationMethod);
            Assert.AreEqual(HttpStatusCode.OK, (await server.GetMeAsync(refreshed.AccessToken)).StatusCode);
        }

        [TestMethod]
        public async Task Connect_ClientSecretPost_Works()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback", OAuthClientAuthenticationMethod.ClientSecretPost);

            OAuthTestTokens tokens = await server.OAuth.ConnectAsync(client, server.SignIn());

            Assert.AreEqual(HttpStatusCode.OK, (await server.GetMeAsync(tokens.AccessToken)).StatusCode);
        }

        [TestMethod]
        public async Task Connect_LoopbackRedirectOnAnotherPort_IsAccepted()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            (_, OAuthTestAuthorization authorization, string code) = await server.ApprovePublicClientAsync("http://127.0.0.1/callback", redirectUri: "http://127.0.0.1:53682/callback");
            OAuthTestTokens tokens = await server.OAuth.RedeemCodeAsync(authorization, code);

            Assert.AreEqual("http://127.0.0.1:53682/callback", server.Store.AuthorizationCodes.Single().RedirectUri);
            Assert.AreEqual(HttpStatusCode.OK, (await server.GetMeAsync(tokens.AccessToken)).StatusCode);
        }

        [TestMethod]
        public async Task Deny_SendsAccessDeniedAndCompletesTheRequest()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);

            string? error = await server.OAuth.DenyAsync(authorization, server.SignIn());

            Assert.AreEqual("access_denied", error);
            OAuthTestFlowException approveAfter = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.ApproveAsync(authorization, server.SignIn()));
            Assert.AreEqual(HttpStatusCode.NotFound, approveAfter.StatusCode);
            Assert.AreEqual(0, server.Handler.ApprovalCalls.Count);
        }

        [TestMethod]
        public async Task Approve_HandlerRefuses_Is403AndTheRequestStaysPending()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            server.Handler.Approve = _ => OAuthApprovalDecision.Refuse("Connections are off for this organization.");

            OAuthTestFlowException refused = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.ApproveAsync(authorization, server.SignIn(), new { organization = "org-off" }));

            Assert.AreEqual(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.AreEqual("Connections are off for this organization.", refused.ErrorDescription);
            server.Handler.Approve = context => OAuthApprovalDecision.Approve(new OAuthGrant { GrantId = "g", Subject = "person-1", AuthType = "user" });
            await server.OAuth.ApproveAsync(authorization, server.SignIn(), new { organization = "org-on" });
            Assert.AreEqual(2, server.Handler.ApprovalCalls.Count);
            Assert.AreEqual("g", server.Store.AuthorizationCodes.Single().GrantId);
        }

        [TestMethod]
        public async Task Approve_Twice_SecondIs404()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            await server.OAuth.ApproveAsync(authorization, server.SignIn());

            OAuthTestFlowException second = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.ApproveAsync(authorization, server.SignIn()));

            Assert.AreEqual(HttpStatusCode.NotFound, second.StatusCode);
        }

        [TestMethod]
        public async Task Approve_DoubleSubmitted_AHandlerKeyedByTheRequestIdRecordsOneGrant()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            ConcurrentDictionary<string, OAuthGrant> grantsByRequest = new ConcurrentDictionary<string, OAuthGrant>();
            using Barrier bothInTheHandler = new Barrier(2);
            bool approvalsOverlapped = true;
            server.Handler.Approve = context =>
            {
                OAuthGrant grant = grantsByRequest.GetOrAdd(context.RequestId, _ => new OAuthGrant { GrantId = "grant-" + Guid.NewGuid().ToString("N"), Subject = "person-1", AuthType = "user" });
                if (!bothInTheHandler.SignalAndWait(TimeSpan.FromSeconds(10)))
                {
                    approvalsOverlapped = false;
                }

                return OAuthApprovalDecision.Approve(grant);
            };
            string signIn = server.SignIn();
            string approvePath = HostedOAuthServer.ApprovePath(authorization);

            HttpResponseMessage[] responses = await Task.WhenAll(server.PostAsync(approvePath, signIn), server.PostAsync(approvePath, signIn));
            HttpStatusCode[] statuses = responses.Select(response => response.StatusCode).ToArray();
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }

            Assert.IsTrue(approvalsOverlapped, "The two approvals did not overlap in the handler, so the test proved nothing.");
            CollectionAssert.AreEquivalent(new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }, statuses);
            Assert.AreEqual(2, server.Handler.ApprovalCalls.Count);
            Assert.AreEqual(1, grantsByRequest.Count);
            Assert.AreEqual(grantsByRequest.Values.Single().GrantId, server.Store.AuthorizationCodes.Single().GrantId);
        }

        [TestMethod]
        public async Task Approve_WithoutTheAppsSignIn_Is401()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);

            using HttpResponseMessage response = await server.Client.PostAsync(HostedOAuthServer.ApprovePath(authorization), null);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(0, server.Handler.ApprovalCalls.Count);
        }

        [TestMethod]
        public async Task Approve_ExpiredRequest_Is404()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);
            server.Clock.Advance(HostedOAuthServer.DefaultOptions().AuthorizationRequestLifetime);

            OAuthTestFlowException expired = await Assert.ThrowsExceptionAsync<OAuthTestFlowException>(
                () => server.OAuth.ApproveAsync(authorization, server.SignIn()));

            Assert.AreEqual(HttpStatusCode.NotFound, expired.StatusCode);
        }

        [TestMethod]
        public async Task Approve_RedirectCarriesStateAndIssuer()
        {
            await using HostedOAuthServer server = await HostedOAuthServer.StartAsync();
            OAuthTestRegisteredClient client = await server.OAuth.RegisterAsync("https://client.example/callback?tenant=a");
            OAuthTestAuthorization authorization = await server.OAuth.AuthorizeAsync(client);

            using HttpResponseMessage response = await server.PostAsync(HostedOAuthServer.ApprovePath(authorization), server.SignIn());
            string redirect = OAuthConsentRedirect.FromJson(await response.Content.ReadAsStringAsync()).RedirectUri;

            Assert.IsTrue(redirect.StartsWith("https://client.example/callback?tenant=a&code=", StringComparison.Ordinal), redirect);
            StringAssert.Contains(redirect, "&state=" + Uri.EscapeDataString(authorization.State));
            StringAssert.Contains(redirect, "&iss=" + Uri.EscapeDataString(HostedOAuthServer.Issuer));
            Assert.AreEqual("no-store", response.Headers.CacheControl!.ToString());
        }

        private static async Task<JsonElement> ReadMeAsync(HostedOAuthServer server, string accessToken)
        {
            using HttpResponseMessage response = await server.GetMeAsync(accessToken);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.Clone();
        }
    }
}
