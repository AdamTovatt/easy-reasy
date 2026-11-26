using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// What completion checks and in what order: the state, the binding cookie, where the response arrived and its
    /// <c>iss</c>, the provider, the code, the ID token and the flow's rules, against the fake issuers.
    /// </summary>
    [TestClass]
    public class OpenIdConnectAuthServiceCompleteTests
    {
        private static readonly string SigningRedirectUri = OpenIdConnectAuthHarness.SigningRedirectUri(OpenIdConnectAuthHarness.PrimaryKey);

        [TestMethod]
        public async Task Complete_ValidFlow_ReturnsTheIdentityAndTheIdToken()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            DateTimeOffset signedInAt = harness.Clock.GetUtcNow();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(nonce: "record-hash"));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            OpenIdConnectIdentity expected = new OpenIdConnectIdentity(harness.Primary.Issuer, FakeOpenIdConnectIssuer.Subject);
            Assert.AreEqual(expected, result.AttemptedIdentity);
            Assert.AreEqual(expected, result.UserInfo.Identity);
            Assert.AreEqual(OpenIdConnectAuthHarness.PrimaryKey, result.UserInfo.ProviderKey);
            Assert.AreEqual(signedInAt, result.UserInfo.AuthenticationTime);
            Assert.AreEqual("record-hash", result.UserInfo.Nonce);
            Assert.AreEqual("urn:mfa", result.UserInfo.AuthenticationContextClass);
            CollectionAssert.AreEqual(new[] { "pwd", "otp" }, result.UserInfo.AuthenticationMethods.ToArray());
            Assert.AreEqual("ada@example.test", result.UserInfo.Email);
            Assert.IsTrue(result.UserInfo.EmailVerified);
            Assert.AreEqual("Ada Lovelace", result.UserInfo.Name);
            CollectionAssert.AreEqual(new[] { "record-hash" }, result.UserInfo.GetClaimValues("nonce").ToArray());
            Assert.AreEqual(harness.Primary.RedeemedIdTokens.Single(), result.IdToken);
        }

        [TestMethod]
        public async Task Complete_ValidFlow_ReturnsThePublicKeyTheTokenVerifiedWith()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            RsaSecurityKey published = (RsaSecurityKey)harness.Primary.SigningKey;

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            using JsonDocument key = JsonDocument.Parse(result.IdTokenSigningKey);
            Assert.AreEqual("RSA", key.RootElement.GetProperty("kty").GetString());
            Assert.AreEqual(published.KeyId, key.RootElement.GetProperty("kid").GetString());
            Assert.AreEqual(Base64UrlEncoder.Encode(published.Rsa.ExportParameters(false).Modulus), key.RootElement.GetProperty("n").GetString());
            Assert.IsFalse(key.RootElement.TryGetProperty("d", out _));
            Assert.IsFalse(key.RootElement.TryGetProperty("p", out _));
        }

        [TestMethod]
        public async Task Complete_TokenSignedWithAnAllowedEcKey_CompletesAndReturnsTheEcPublicKey()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            ECDsaSecurityKey ecKey = FakeOpenIdConnectIssuer.CreateEcKey("ec-key");
            harness.Primary.PublishedKeys = [harness.Primary.SigningKey, ecKey];
            harness.ChangeProvider(OpenIdConnectAuthHarness.PrimaryKey, signingAlgorithms: [SecurityAlgorithms.EcdsaSha256]);

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token =>
            {
                token.SigningKey = ecKey;
                token.Algorithm = SecurityAlgorithms.EcdsaSha256;
            });

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            using JsonDocument key = JsonDocument.Parse(result.IdTokenSigningKey);
            Assert.AreEqual("EC", key.RootElement.GetProperty("kty").GetString());
            Assert.AreEqual("P-256", key.RootElement.GetProperty("crv").GetString());
            Assert.IsFalse(key.RootElement.TryGetProperty("d", out _));
        }

        [TestMethod]
        public async Task Complete_TokenSignedWithAnAlgorithmOutsideTheProvidersList_IsRefusedAsInvalidSignature()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Algorithm = SecurityAlgorithms.RsaSha384);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidSignature, result.FailureReason);
            Assert.IsNull(result.AttemptedIdentity);
        }

        [TestMethod]
        [DataRow("ToString")]
        [DataRow("Json")]
        public async Task Complete_ValidFlow_LeavesTheIdTokenCallerBindingAndIdentityDetailsOutOfItsLoggableForm(string form)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());
            string text = form == "Json" ? JsonSerializer.Serialize(result) : result.ToString();

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            Assert.IsFalse(text.Contains(result.IdToken, StringComparison.Ordinal), text);
            Assert.IsFalse(text.Contains(result.UserInfo.Email!, StringComparison.Ordinal), text);
            Assert.IsFalse(text.Contains(result.UserInfo.Name!, StringComparison.Ordinal), text);
            StringAssert.Contains(text, FakeOpenIdConnectIssuer.Subject);
        }

        [TestMethod]
        public async Task Complete_TokenWithoutOptionalClaims_ReturnsNullsAndEmptyLists()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token =>
            {
                foreach (string claim in new[] { "auth_time", "acr", "amr", "email", "email_verified", "name" })
                {
                    token.Claims.Remove(claim);
                }
            });

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            Assert.IsNull(result.UserInfo.AuthenticationTime);
            Assert.IsNull(result.UserInfo.AuthenticationContextClass);
            Assert.AreEqual(0, result.UserInfo.AuthenticationMethods.Count);
            Assert.IsNull(result.UserInfo.Email);
            Assert.IsFalse(result.UserInfo.EmailVerified);
            Assert.IsNull(result.UserInfo.Name);
        }

        [TestMethod]
        public async Task Complete_EmailVerifiedSentAsAString_IsRead()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims["email_verified"] = "true");

            Assert.IsTrue(result.UserInfo!.EmailVerified);
        }

        [TestMethod]
        public async Task Complete_ClientSecretBasic_SendsTheCredentialsInTheHeaderOnly()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            // The fake refuses credentials sent any other way than the client is registered, so success proves them.
            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            FakeTokenRequest request = harness.Primary.TokenRequests.Single();
            StringAssert.StartsWith(request.Authorization, "Basic ");
            Assert.IsFalse(request.Form.ContainsKey("client_id"));
            Assert.IsFalse(request.Form.ContainsKey("client_secret"));
            Assert.AreEqual(SigningRedirectUri, request.Form["redirect_uri"]);
            Assert.AreEqual("authorization_code", request.Form["grant_type"]);
        }

        [TestMethod]
        public async Task Complete_ClientSecretPost_SendsTheCredentialsInTheBodyOnly()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.ClientAuthentication = OpenIdConnectClientAuthentication.ClientSecretPost;
            harness.Resolver.Providers[OpenIdConnectAuthHarness.PrimaryKey] = OpenIdConnectAuthHarness.CreateProvider(harness.Primary, OpenIdConnectAuthHarness.PrimaryKey);

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            FakeTokenRequest request = harness.Primary.TokenRequests.Single();
            Assert.IsNull(request.Authorization);
            Assert.AreEqual(FakeOpenIdConnectIssuer.ClientSecret, request.Form["client_secret"]);
        }

        [TestMethod]
        public async Task Complete_ClearsTheFlowsBindingCookieWhetherItSucceedsOrNot()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult succeeding = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            OpenIdConnectAuthBeginResult refused = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromSeconds(1)), browser);
            harness.Clock.Advance(TimeSpan.FromSeconds(5));

            OpenIdConnectAuthCompletionResult succeeded = await harness.CompleteBegunAsync(succeeding, browser);
            OpenIdConnectAuthCompletionResult stale = await harness.CompleteAsync(
                OpenIdConnectAuthHarness.CreateResponse(harness.Primary.IssueCode(refused.AuthorizationUrl, token => token.Claims["auth_time"] = harness.Clock.GetUtcNow().AddMinutes(-1).ToUnixTimeSeconds()), refused.State, SigningRedirectUri),
                browser);

            Assert.IsTrue(succeeded.Success, succeeded.FailureReason?.ToString());
            Assert.AreEqual(OpenIdConnectAuthFailureReason.StaleAuthTime, stale.FailureReason);
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Complete_SameResponseTwice_SecondIsRefusedForTheMissingCookieWithoutCallingTheProvider()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            OpenIdConnectAuthorizationResponse response = OpenIdConnectAuthHarness.CreateResponse(harness.Primary.IssueCode(begun.AuthorizationUrl), begun.State, SigningRedirectUri);

            OpenIdConnectAuthCompletionResult first = await harness.CompleteAsync(response, browser);
            OpenIdConnectAuthCompletionResult second = await harness.CompleteAsync(response, browser);

            Assert.IsTrue(first.Success, first.FailureReason?.ToString());
            Assert.AreEqual(OpenIdConnectAuthFailureReason.BindingCookieMissing, second.FailureReason);
            Assert.AreEqual(1, harness.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_ReplayedFromACopyOfTheBrowserTakenBeforeCompletion_IsRefusedByTheProviderAsCodeRedemptionFailed()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            FakeBrowser copy = browser.Clone();

            OpenIdConnectAuthorizationResponse response = OpenIdConnectAuthHarness.CreateResponse(harness.Primary.IssueCode(begun.AuthorizationUrl), begun.State, SigningRedirectUri);
            OpenIdConnectAuthCompletionResult first = await harness.CompleteAsync(response, browser);
            OpenIdConnectAuthCompletionResult replay = await harness.CompleteAsync(response, copy);

            // No store of used flows: the provider's single use of a code is what refuses the replay.
            Assert.IsTrue(first.Success, first.FailureReason?.ToString());
            Assert.AreEqual(OpenIdConnectAuthFailureReason.CodeRedemptionFailed, replay.FailureReason);
            Assert.AreEqual(2, harness.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_TwoFlowsBegunInOneBrowser_BothComplete()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult first = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            OpenIdConnectAuthBeginResult second = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            OpenIdConnectAuthCompletionResult secondResult = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse(harness.Primary.IssueCode(second.AuthorizationUrl), second.State, SigningRedirectUri), browser);
            OpenIdConnectAuthCompletionResult firstResult = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse(harness.Primary.IssueCode(first.AuthorizationUrl), first.State, SigningRedirectUri), browser);

            Assert.IsTrue(firstResult.Success, firstResult.FailureReason?.ToString());
            Assert.IsTrue(secondResult.Success, secondResult.FailureReason?.ToString());
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("not*base64url")]
        [DataRow("AAAA")]
        public async Task Complete_StateThatIsNotThisServers_IsRefusedAsInvalidStateWithoutCallingAnyone(string state)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse("code", state, SigningRedirectUri), harness.CreateBrowser());

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidState, result.FailureReason);
            Assert.AreEqual(0, harness.Network.RequestCount);
            Assert.AreEqual(0, harness.Resolver.RequestedKeys.Count);
        }

        [TestMethod]
        public async Task Complete_TamperedState_IsRefusedAsInvalidState()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            char[] tampered = begun.State.ToCharArray();
            tampered[tampered.Length / 2] = tampered[tampered.Length / 2] == 'A' ? 'B' : 'A';

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(
                OpenIdConnectAuthHarness.CreateResponse(harness.Primary.IssueCode(begun.AuthorizationUrl), new string(tampered), SigningRedirectUri), browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidState, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_StateSealedUnderAnotherKey_IsRefusedAsInvalidState()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            using OpenIdConnectAuthHarness otherServer = OpenIdConnectAuthHarness.Create(OpenIdConnectAuthHarness.CreateOptions(stateKey: Enumerable.Repeat((byte)7, 32).ToArray()));
            FakeBrowser browser = otherServer.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await otherServer.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(
                OpenIdConnectAuthHarness.CreateResponse(otherServer.Primary.IssueCode(begun.AuthorizationUrl), begun.State, SigningRedirectUri), browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidState, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_StateOlderThanItsLifetime_IsRefusedAsExpiredState()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            string code = harness.Primary.IssueCode(begun.AuthorizationUrl);
            harness.Clock.Advance(TimeSpan.FromMinutes(10));

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse(code, begun.State, SigningRedirectUri), browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.ExpiredState, result.FailureReason);
            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_StateJustInsideItsLifetime_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            string code = harness.Primary.IssueCode(begun.AuthorizationUrl);
            harness.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse(code, begun.State, SigningRedirectUri), browser);

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_FromABrowserWithoutTheBindingCookie_IsRefusedAsBindingCookieMissing()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), harness.CreateBrowser());

            OpenIdConnectAuthCompletionResult result = await harness.CompleteBegunAsync(begun, harness.CreateBrowser());

            Assert.AreEqual(OpenIdConnectAuthFailureReason.BindingCookieMissing, result.FailureReason);
            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_WithAnotherValueInTheBindingCookie_IsRefusedAsBindingCookieMismatch()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            browser.Set(browser.Cookies.Keys.Single(), "planted-value");

            OpenIdConnectAuthCompletionResult result = await harness.CompleteBegunAsync(begun, browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.BindingCookieMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_ResponseFromAnotherProviderArrivingAtItsOwnRedirectUri_IsRefusedAsRedirectUriMismatchBeforeAnyRedemption()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            // The person began with the other provider, which sent the browser on to the primary provider's authorize
            // endpoint with the primary client's id; the primary provider redirects only to its own redirect URI.
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(providerKey: OpenIdConnectAuthHarness.OtherKey), browser);
            OpenIdConnectAuthBeginResult primaryFlow = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), harness.CreateBrowser());
            string primaryCode = harness.Primary.IssueCode(primaryFlow.AuthorizationUrl);

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(
                OpenIdConnectAuthHarness.CreateResponse(primaryCode, begun.State, SigningRedirectUri), browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.RedirectUriMismatch, result.FailureReason);
            Assert.AreEqual(0, harness.Other.TokenRequests.Count);
            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_FlowBegunWithAnotherCallerBinding_IsRefusedAsCallerBindingMismatchBeforeAnyRedemption()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            string code = harness.Primary.IssueCode(begun.AuthorizationUrl);
            string anotherRecordsBinding = "user-7:record-10";

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(
                OpenIdConnectAuthHarness.CreateResponse(code, begun.State, SigningRedirectUri), browser, expectedCallerBinding: anotherRecordsBinding);

            Assert.AreNotEqual(OpenIdConnectAuthHarness.CallerBinding, anotherRecordsBinding);
            Assert.AreEqual(OpenIdConnectAuthFailureReason.CallerBindingMismatch, result.FailureReason);
            Assert.IsNull(result.AttemptedIdentity);
            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
            Assert.AreEqual(0, OpenIdConnectAuthHarness.CountBindingCookies(browser));
        }

        [TestMethod]
        public async Task Complete_IssuerParameterOfAnotherProvider_IsRefusedAsIssuerParameterMismatchBeforeAnyRedemption()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            OpenIdConnectAuthCompletionResult result = await harness.CompleteBegunAsync(begun, browser, responseIssuer: harness.Other.Issuer);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.IssuerParameterMismatch, result.FailureReason);
            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_ProviderAdvertisingTheIssuerParameterAndAResponseWithout_IsRefusedAsIssuerParameterMissing()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.AdvertisesIssuerParameter = true;
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            OpenIdConnectAuthCompletionResult result = await harness.CompleteBegunAsync(begun, browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.IssuerParameterMissing, result.FailureReason);
            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_ProviderAdvertisingTheIssuerParameterAndTheRightOne_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.AdvertisesIssuerParameter = true;

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_ProviderKeyNoLongerResolving_IsRefusedAsUnknownProvider()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            string code = harness.Primary.IssueCode(begun.AuthorizationUrl);
            harness.Resolver.Providers.TryRemove(OpenIdConnectAuthHarness.PrimaryKey, out _);

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse(code, begun.State, SigningRedirectUri), browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.UnknownProvider, result.FailureReason);
        }

        [TestMethod]
        [DataRow("issuer")]
        [DataRow("client")]
        public async Task Complete_ProviderChangedSinceBegin_IsRefusedAsProviderChangedWithoutRedeemingAnywhere(string change)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            string code = harness.Primary.IssueCode(begun.AuthorizationUrl);
            harness.ChangeProvider(
                OpenIdConnectAuthHarness.PrimaryKey,
                issuer: change == "issuer" ? OpenIdConnectIssuer.Fixed(harness.Other.Issuer) : null,
                clientId: change == "client" ? "another-client" : null);

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse(code, begun.State, SigningRedirectUri), browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.ProviderChanged, result.FailureReason);
            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
            Assert.AreEqual(0, harness.Other.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_EmptyCode_IsRefusedAsCodeRedemptionFailedWithoutCallingTheProvider()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse("", begun.State, SigningRedirectUri), browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.CodeRedemptionFailed, result.FailureReason);
            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.BadRequest, "invalid_grant")]
        [DataRow(HttpStatusCode.BadRequest, "invalid_request")]
        public async Task Complete_TokenEndpointRefusingTheCode_IsRefusedAsCodeRedemptionFailed(HttpStatusCode status, string error)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.TokenEndpointStatusOverride = status;
            harness.Primary.TokenEndpointErrorOverride = error;

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());

            Assert.AreEqual(OpenIdConnectAuthFailureReason.CodeRedemptionFailed, result.FailureReason);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.Unauthorized, "invalid_client")]
        [DataRow(HttpStatusCode.BadRequest, "unauthorized_client")]
        [DataRow(HttpStatusCode.BadRequest, null)]
        [DataRow(HttpStatusCode.Unauthorized, null)]
        public async Task Complete_TokenEndpointRefusingTheClient_ThrowsInvalidOperationInsteadOfReportingARefusal(HttpStatusCode status, string? error)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.TokenEndpointStatusOverride = status;
            harness.Primary.TokenEndpointErrorOverride = error;

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest()));

            StringAssert.Contains(exception.Message, error ?? "(none)");
        }

        [TestMethod]
        public async Task Complete_WrongClientSecret_ThrowsInvalidOperation()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.ChangeProvider(OpenIdConnectAuthHarness.PrimaryKey, clientSecret: "expired-secret");

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest()));
        }

        [TestMethod]
        [DataRow(HttpStatusCode.InternalServerError)]
        [DataRow(HttpStatusCode.TooManyRequests)]
        public async Task Complete_TokenEndpointServerErrorOrThrottle_ThrowsHttpRequestException(HttpStatusCode status)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.TokenEndpointStatusOverride = status;

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest()));
        }

        [TestMethod]
        [DataRow("{}")]
        [DataRow("{\"id_token\":\"\"}")]
        [DataRow("{\"id_token\":7}")]
        [DataRow("not json")]
        public async Task Complete_TokenEndpointSuccessWithoutAUsableIdToken_IsRefusedAsCodeRedemptionFailed(string body)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.TokenEndpointSuccessBodyOverride = body;

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());

            Assert.AreEqual(OpenIdConnectAuthFailureReason.CodeRedemptionFailed, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenFromAnotherIssuer_IsRefusedAsInvalidIssuer()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims["iss"] = harness.Other.Issuer);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidIssuer, result.FailureReason);
            Assert.IsNull(result.AttemptedIdentity);
        }

        [TestMethod]
        public async Task Complete_TokenForAnotherAudience_IsRefusedAsInvalidAudience()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims["aud"] = "another-client");

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidAudience, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenNamingAnotherAuthorizedParty_IsRefusedAsInvalidAudience()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims["azp"] = "another-client");

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidAudience, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenForSeveralAudiencesWithoutAnAuthorizedParty_IsRefusedAsInvalidAudience()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims["aud"] = new[] { FakeOpenIdConnectIssuer.ClientId, "another-client" });

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidAudience, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenForSeveralAudiencesNamingThisClientAsAuthorizedParty_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token =>
            {
                token.Claims["aud"] = new[] { FakeOpenIdConnectIssuer.ClientId, "another-client" };
                token.Claims["azp"] = FakeOpenIdConnectIssuer.ClientId;
            });

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_TokenWithAnotherNonce_IsRefusedAsNonceMismatch()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims["nonce"] = "another-nonce");

            Assert.AreEqual(OpenIdConnectAuthFailureReason.NonceMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenWithoutNonce_IsRefusedAsNonceMismatch()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims.Remove("nonce"));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.NonceMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenSignedByAnUnpublishedKey_IsRefusedAsInvalidSignature()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.SigningKey = FakeOpenIdConnectIssuer.CreateRsaKey("unpublished"));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidSignature, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenSignedByAnotherKeyUnderThePublishedKeyId_IsRefusedAsInvalidSignature()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(
                OpenIdConnectAuthHarness.CreateRequest(),
                token => token.SigningKey = FakeOpenIdConnectIssuer.CreateRsaKey(harness.Primary.SigningKey.KeyId));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidSignature, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_ExpiredToken_IsRefusedAsTokenExpired()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            long longAgo = harness.Clock.GetUtcNow().AddHours(-2).ToUnixTimeSeconds();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token =>
            {
                token.Claims["iat"] = longAgo;
                token.Claims["nbf"] = longAgo;
                token.Claims["exp"] = longAgo + 60;
            });

            Assert.AreEqual(OpenIdConnectAuthFailureReason.TokenExpired, result.FailureReason);
        }

        [TestMethod]
        [DataRow("exp")]
        [DataRow("sub")]
        public async Task Complete_TokenMissingARequiredClaim_IsRefusedAsInvalidToken(string claim)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims.Remove(claim));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidToken, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenWithANonNumericAuthTime_IsRefusedAsInvalidToken()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.Claims["auth_time"] = "yesterday");

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidToken, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_DiscoveryNamingAnHttpKeySet_RefusesToFetchItAndThrows()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.JwksUri = "http://primary.fake.test/keys";

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest()));

            Assert.AreEqual(0, harness.Primary.KeysRequestCount);
        }

        [TestMethod]
        public async Task Complete_RemoteDiscoveryNamingAPlainHttpLoopbackKeySet_RefusesToFetchItAndThrows()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.JwksUri = "http://127.0.0.1/keys";

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest()));

            Assert.AreEqual(0, harness.Network.UnroutedRequests.Count, string.Join(", ", harness.Network.UnroutedRequests));
        }

        [TestMethod]
        [DataRow("http://primary.fake.test/token")]
        [DataRow("http://127.0.0.1/token")]
        public async Task Complete_RemoteDiscoveryNamingAPlainHttpTokenEndpoint_RefusesToSendTheCodeAndThrows(string tokenEndpoint)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.TokenEndpoint = tokenEndpoint;

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest()));

            Assert.AreEqual(0, harness.Primary.TokenRequests.Count);
            Assert.AreEqual(0, harness.Network.UnroutedRequests.Count, string.Join(", ", harness.Network.UnroutedRequests));
        }

        [TestMethod]
        public async Task Complete_SeveralFlows_FetchTheDiscoveryDocumentAndKeysOnce()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            for (int flow = 0; flow < 3; flow++)
            {
                OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());
                Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            }

            Assert.AreEqual(1, harness.Primary.MetadataRequestCount);
            Assert.AreEqual(1, harness.Primary.KeysRequestCount);
        }

        [TestMethod]
        public async Task Complete_TokenSignedByAKeyPublishedAfterTheCachedSet_FetchesTheKeysAfreshAndCompletes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());
            RsaSecurityKey rotated = FakeOpenIdConnectIssuer.CreateRsaKey("rotated-key");
            harness.Primary.PublishedKeys = [rotated];

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.SigningKey = rotated);

            // Only the fresh set holds the rotated key, so success proves the keys were fetched afresh.
            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            StringAssert.Contains(result.IdTokenSigningKey, "rotated-key");
        }

        [TestMethod]
        public async Task Complete_KeyInNeitherTheCachedNorTheFreshSet_IsRefusedAsInvalidSignatureWithoutARetryLoop()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest());
            int keysRequestsBefore = harness.Primary.KeysRequestCount;

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(), token => token.SigningKey = FakeOpenIdConnectIssuer.CreateRsaKey("never-published"));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidSignature, result.FailureReason);
            // One forced fetch, plus at most the cache's own background refresh: no retry loop.
            int keysRequests = harness.Primary.KeysRequestCount - keysRequestsBefore;
            Assert.IsTrue(keysRequests >= 1 && keysRequests <= 2, $"{keysRequests} key set requests");
        }

        [TestMethod]
        public async Task Complete_ExpectedIdentityAndAnotherAccount_IsRefusedAsIdentityMismatchNamingTheAccount()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            OpenIdConnectIdentity expected = new OpenIdConnectIdentity(harness.Primary.Issuer, "subject-grace");

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(expectedIdentity: expected));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.IdentityMismatch, result.FailureReason);
            Assert.AreEqual(new OpenIdConnectIdentity(harness.Primary.Issuer, FakeOpenIdConnectIssuer.Subject), result.AttemptedIdentity);
            Assert.IsNull(result.UserInfo);
            Assert.IsNull(result.IdToken);
        }

        [TestMethod]
        public async Task Complete_ExpectedIdentityAndThatAccount_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(
                expectedIdentity: new OpenIdConnectIdentity(harness.Primary.Issuer, FakeOpenIdConnectIssuer.Subject)));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_MaxAgeAndNoAuthTime_IsRefusedAsMissingAuthTime()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromMinutes(5)), token => token.Claims.Remove("auth_time"));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.MissingAuthTime, result.FailureReason);
            Assert.AreEqual(new OpenIdConnectIdentity(harness.Primary.Issuer, FakeOpenIdConnectIssuer.Subject), result.AttemptedIdentity);
        }

        [TestMethod]
        public async Task Complete_MaxAgeAndAnAuthTimeOlderThanIt_IsRefusedAsStaleAuthTime()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            long tooOld = harness.Clock.GetUtcNow().AddMinutes(-5).AddSeconds(-1).ToUnixTimeSeconds();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromMinutes(5)), token => token.Claims["auth_time"] = tooOld);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.StaleAuthTime, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_MaxAgeAndAnAuthTimeExactlyAtIt_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            long atLimit = harness.Clock.GetUtcNow().AddMinutes(-5).ToUnixTimeSeconds();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromMinutes(5)), token => token.Claims["auth_time"] = atLimit);

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_FractionalMaxAge_AppliesTheWholeSecondsBeginSent()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            long elevenSecondsAgo = harness.Clock.GetUtcNow().AddSeconds(-11).ToUnixTimeSeconds();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromSeconds(10.9)), token => token.Claims["auth_time"] = elevenSecondsAgo);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.StaleAuthTime, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_AcceptedAcrValuesAndNoAcr_IsRefusedAsMissingAcr()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(acceptedAcrValues: ["urn:mfa"]), token => token.Claims.Remove("acr"));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.MissingAcr, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_AcceptedAcrValuesAndAnotherAcr_IsRefusedAsAcrNotAccepted()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(acceptedAcrValues: ["urn:hwk", "URN:MFA"]));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.AcrNotAccepted, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_AcceptedAcrValuesHoldingTheTokensAcr_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(acceptedAcrValues: ["urn:hwk", "urn:mfa"]));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_AcceptedAmrValuesAndNoAmr_IsRefusedAsMissingAmr()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(acceptedAmrValues: ["otp"]), token => token.Claims.Remove("amr"));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.MissingAmr, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_AcceptedAmrValuesAndNoneOfThem_IsRefusedAsAmrNotAccepted()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(acceptedAmrValues: ["hwk", "face"]));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.AmrNotAccepted, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_AcceptedAmrValuesHoldingOneOfTheTokens_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(acceptedAmrValues: ["hwk", "otp"]));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_AcceptedAcrAndAmrValuesWithOnlyTheAcrMatching_IsRefusedAsAmrNotAccepted()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(acceptedAcrValues: ["urn:mfa"], acceptedAmrValues: ["hwk"]));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.AmrNotAccepted, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_RequiredClaimTheTokenLacks_IsRefusedAsMissingRequiredClaimWithTheAccount()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(
                OpenIdConnectAuthHarness.CreateRequest(requiredClaimValues: new Dictionary<string, string> { ["acrs"] = "c1" }));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.MissingRequiredClaim, result.FailureReason);
            Assert.AreEqual(new OpenIdConnectIdentity(harness.Primary.Issuer, FakeOpenIdConnectIssuer.Subject), result.AttemptedIdentity);
        }

        [TestMethod]
        [DataRow("c2")]
        [DataRow("C1")]
        public async Task Complete_RequiredClaimWithAnotherValue_IsRefusedAsRequiredClaimMismatch(string value)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(
                OpenIdConnectAuthHarness.CreateRequest(requiredClaimValues: new Dictionary<string, string> { ["acrs"] = "c1" }),
                token => token.Claims["acrs"] = value);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.RequiredClaimMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_RequiredClaimArrayWithoutTheValue_IsRefusedAsRequiredClaimMismatch()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(
                OpenIdConnectAuthHarness.CreateRequest(requiredClaimValues: new Dictionary<string, string> { ["acrs"] = "c1" }),
                token => token.Claims["acrs"] = new[] { "c2", "c3" });

            Assert.AreEqual(OpenIdConnectAuthFailureReason.RequiredClaimMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_RequiredClaimEqualToTheValueOrAnArrayHoldingIt_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            Dictionary<string, string> required = new Dictionary<string, string> { ["acrs"] = "c1", ["groups"] = "admins" };

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(
                OpenIdConnectAuthHarness.CreateRequest(requiredClaimValues: required),
                token =>
                {
                    token.Claims["acrs"] = new[] { "c2", "c1" };
                    token.Claims["groups"] = "admins";
                });

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_RequiredBooleanAndNumberClaims_MatchTheirJsonText()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            Dictionary<string, string> required = new Dictionary<string, string> { ["mfa"] = "true", ["level"] = "7" };

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(
                OpenIdConnectAuthHarness.CreateRequest(requiredClaimValues: required),
                token =>
                {
                    token.Claims["mfa"] = true;
                    token.Claims["level"] = 7;
                });

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_TokenLifetime_IsCheckedOnTheRegisteredClock()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            string code = harness.Primary.IssueCode(begun.AuthorizationUrl, token => token.Claims["exp"] = harness.Clock.GetUtcNow().AddMinutes(1).ToUnixTimeSeconds());
            harness.Clock.Advance(TimeSpan.FromMinutes(7));

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(OpenIdConnectAuthHarness.CreateResponse(code, begun.State, SigningRedirectUri), browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.TokenExpired, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_MultiTenantProvider_CompletesWithTheTokensOwnTenantIssuer()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(providerKey: OpenIdConnectAuthHarness.TenantsKey));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            Assert.AreEqual(harness.Tenants.IssuerForTenant(FakeOpenIdConnectIssuer.TenantId), result.UserInfo.Identity.Issuer);
            Assert.AreEqual(FakeOpenIdConnectIssuer.TenantId, result.UserInfo.FindClaimValue("tid"));
        }

        [TestMethod]
        public async Task Complete_MultiTenantProviderAdvertisingTheIssParameterAndTheTokensTenantIssuer_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Tenants.AdvertisesIssuerParameter = true;

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(providerKey: OpenIdConnectAuthHarness.TenantsKey));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_MultiTenantProviderAndAnIssuerOfAnotherTenantThanTheTokensTid_IsRefusedAsInvalidIssuer()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(
                OpenIdConnectAuthHarness.CreateRequest(providerKey: OpenIdConnectAuthHarness.TenantsKey),
                token => token.Claims["iss"] = harness.Tenants.IssuerForTenant("tenant-b"));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidIssuer, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_MultiTenantProviderAndATokenWithoutTheTenantClaim_IsRefusedAsInvalidIssuer()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(
                OpenIdConnectAuthHarness.CreateRequest(providerKey: OpenIdConnectAuthHarness.TenantsKey),
                token => token.Claims.Remove("tid"));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.InvalidIssuer, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_MultiTenantProviderAndAnExpectedIdentityAtAnotherTenant_IsRefusedAsIdentityMismatch()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(
                providerKey: OpenIdConnectAuthHarness.TenantsKey,
                expectedIdentity: new OpenIdConnectIdentity(harness.Tenants.IssuerForTenant("tenant-b"), FakeOpenIdConnectIssuer.Subject)));

            Assert.AreEqual(OpenIdConnectAuthFailureReason.IdentityMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_MultiTenantProviderAndAnExpectedIdentityAtTheTokensTenant_Completes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthCompletionResult result = await harness.SignInAsync(OpenIdConnectAuthHarness.CreateRequest(
                providerKey: OpenIdConnectAuthHarness.TenantsKey,
                expectedIdentity: new OpenIdConnectIdentity(harness.Tenants.TokenIssuer, FakeOpenIdConnectIssuer.Subject)));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_MultiTenantProviderAndAnIssuerParameterOfAnotherTenant_IsRefusedAsIssuerParameterMismatch()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            string redirectUri = OpenIdConnectAuthHarness.SigningRedirectUri(OpenIdConnectAuthHarness.TenantsKey);
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(providerKey: OpenIdConnectAuthHarness.TenantsKey), browser);

            OpenIdConnectAuthCompletionResult result = await harness.CompleteAsync(
                OpenIdConnectAuthHarness.CreateResponse(harness.Tenants.IssueCode(begun.AuthorizationUrl), begun.State, redirectUri, harness.Tenants.IssuerForTenant("tenant-b")),
                browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.IssuerParameterMismatch, result.FailureReason);
        }

        [TestMethod]
        public void ToString_SuccessAndFailures_NamesWhatIsKnownAndRedactsTheIdToken()
        {
            OpenIdConnectIdentity identity = new OpenIdConnectIdentity("https://issuer.example", "sub-1");
            OpenIdConnectUserInfo userInfo = new OpenIdConnectUserInfo
            {
                ProviderKey = "primary",
                Identity = identity,
                Nonce = "nonce",
                AuthenticationMethods = [],
                Claims = [],
            };

            Assert.AreEqual(
                $"OpenIdConnectAuthCompletionResult {{ Success = true, AttemptedIdentity = {identity}, IdToken = [REDACTED] }}",
                OpenIdConnectAuthCompletionResult.Succeeded(userInfo, "header.payload.signature", "{}").ToString());
            Assert.AreEqual(
                $"OpenIdConnectAuthCompletionResult {{ Success = false, FailureReason = StaleAuthTime, AttemptedIdentity = {identity} }}",
                OpenIdConnectAuthCompletionResult.Refused(OpenIdConnectAuthFailureReason.StaleAuthTime, identity).ToString());
            Assert.AreEqual(
                "OpenIdConnectAuthCompletionResult { Success = false, FailureReason = InvalidState }",
                OpenIdConnectAuthCompletionResult.Failed(OpenIdConnectAuthFailureReason.InvalidState).ToString());
        }
    }
}
