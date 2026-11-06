using Microsoft.IdentityModel.Tokens;
using System.Buffers.Text;
using System.Net;
using System.Text;

namespace EasyReasy.Auth.Microsoft.Tests
{
    [TestClass]
    public class MicrosoftAuthServiceCompleteTests
    {
        private const string Subject = FakeMicrosoftIdentityProvider.TenantId + ":" + FakeMicrosoftIdentityProvider.ObjectId;

        [TestMethod]
        public async Task Complete_ValidFlow_ReturnsTheIdentityAndTheCallerBinding()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            DateTimeOffset signedInAt = harness.Clock.GetUtcNow();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(nonce: "record-hash"));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            Assert.AreEqual(MicrosoftAuthHarness.CallerBinding, result.CallerBinding);
            Assert.AreEqual(Subject, result.AttemptedSubject);
            Assert.IsNull(result.FailureReason);
            Assert.AreEqual(FakeMicrosoftIdentityProvider.Tenant, result.UserInfo.TenantId);
            Assert.AreEqual(FakeMicrosoftIdentityProvider.ObjectId, result.UserInfo.ObjectId);
            Assert.AreEqual(signedInAt, result.UserInfo.AuthenticationTime);
            Assert.AreEqual("record-hash", result.UserInfo.Nonce);
            Assert.AreEqual("ada@contoso.example", result.UserInfo.PreferredUsername);
            Assert.AreEqual("Ada Lovelace", result.UserInfo.Name);
            CollectionAssert.AreEqual(new[] { "pwd", "mfa" }, result.UserInfo.AuthenticationMethods.ToArray());
            CollectionAssert.AreEqual(new[] { "c1" }, result.UserInfo.AuthenticationContextIds.ToArray());
        }

        [TestMethod]
        public async Task Complete_TokenWithoutOptionalClaims_ReturnsNullsAndEmptyLists()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token =>
            {
                token.Claims.Remove("auth_time");
                token.Claims.Remove("name");
                token.Claims.Remove("preferred_username");
                token.Claims.Remove("amr");
                token.Claims.Remove("acrs");
            });

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            Assert.IsNull(result.UserInfo.AuthenticationTime);
            Assert.IsNull(result.UserInfo.Name);
            Assert.IsNull(result.UserInfo.PreferredUsername);
            Assert.AreEqual(0, result.UserInfo.AuthenticationMethods.Count);
            Assert.AreEqual(0, result.UserInfo.AuthenticationContextIds.Count);
        }

        [TestMethod]
        public async Task Complete_RedeemsTheCodeAsAConfidentialClientWithTheFlowsRedirectUriAndVerifier()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            // The fake refuses a code redeemed with the wrong redirect URI, verifier or credentials, so success proves them.
            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(redirectUri: MicrosoftAuthHarness.SignInRedirectUri));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            Dictionary<string, string> form = harness.IdentityProvider.TokenRequests.Single();
            Assert.AreEqual(MicrosoftAuthHarness.SignInRedirectUri, form["redirect_uri"]);
            Assert.AreEqual(FakeMicrosoftIdentityProvider.ClientSecret, form["client_secret"]);
            Assert.AreEqual("openid profile", form["scope"]);
        }

        [TestMethod]
        public async Task Complete_FlowForATenant_RedeemsOnThatTenantsTokenEndpoint()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            // The fake refuses a code redeemed on another segment than the one it was issued on.
            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(tenantId: FakeMicrosoftIdentityProvider.Tenant));

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_ClearsTheFlowsBindingCookie()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);
            Assert.AreEqual(1, browser.Cookies.Count);

            await harness.CompleteAsync(harness.IdentityProvider.IssueCode(begun.AuthorizationUrl), begun.State, browser);

            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Complete_RefusedFlow_StillClearsTheFlowsBindingCookie()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync("unknown-code", begun.State, browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.CodeRedemptionFailed, result.FailureReason);
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Complete_SameStateTwice_SecondIsRefusedForTheMissingCookie()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);
            await harness.CompleteAsync(harness.IdentityProvider.IssueCode(begun.AuthorizationUrl), begun.State, browser);

            MicrosoftAuthCompletionResult second = await harness.CompleteAsync(harness.IdentityProvider.IssueCode(begun.AuthorizationUrl), begun.State, browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.BindingCookieMissing, second.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TwoFlowsBegunInOneBrowser_BothComplete()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult signIn = harness.Begin(MicrosoftAuthHarness.CreateRequest(redirectUri: MicrosoftAuthHarness.SignInRedirectUri), browser);
            MicrosoftAuthBeginResult signing = harness.Begin(MicrosoftAuthHarness.CreateRequest(requiredAuthenticationContextId: "c1"), browser);
            string signInCode = harness.IdentityProvider.IssueCode(signIn.AuthorizationUrl);
            string signingCode = harness.IdentityProvider.IssueCode(signing.AuthorizationUrl);

            // Completed in the opposite order to the one they were begun in.
            MicrosoftAuthCompletionResult signingResult = await harness.CompleteAsync(signingCode, signing.State, browser);
            MicrosoftAuthCompletionResult signInResult = await harness.CompleteAsync(signInCode, signIn.State, browser);

            Assert.IsTrue(signingResult.Success, signingResult.FailureReason?.ToString());
            Assert.IsTrue(signInResult.Success, signInResult.FailureReason?.ToString());
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Complete_TamperedState_IsRefusedAsInvalidStateWithoutCallingMicrosoft()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);
            string code = harness.IdentityProvider.IssueCode(begun.AuthorizationUrl);
            char[] tampered = begun.State.ToCharArray();
            int middle = tampered.Length / 2;
            tampered[middle] = tampered[middle] == 'A' ? 'B' : 'A';

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync(code, new string(tampered), browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidState, result.FailureReason);
            Assert.IsNull(result.AttemptedSubject);
            Assert.AreEqual(0, harness.IdentityProvider.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_StateSealedUnderAnotherKey_IsRefusedAsInvalidState()
        {
            using MicrosoftAuthHarness otherServer = MicrosoftAuthHarness.Create(
                MicrosoftAuthHarness.CreateOptions(stateKey: Enumerable.Repeat((byte)0x5a, 32).ToArray()));
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = otherServer.Begin(MicrosoftAuthHarness.CreateRequest(), browser);

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync("any-code", begun.State, browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidState, result.FailureReason);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("not base64url!")]
        [DataRow("AAAA")]
        public async Task Complete_EmptyOrNonBase64UrlOrTooShortState_IsRefusedAsInvalidState(string state)
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync("any-code", state, harness.CreateBrowser());

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidState, result.FailureReason);
        }

        [TestMethod]
        [DataRow("{\"unrelated\":true}")]
        [DataRow("[1,2]")]
        [DataRow("null")]
        [DataRow("not json")]
        public async Task Complete_StateSealedUnderTheKeyButNotAState_IsRefusedAsInvalidState(string plaintext)
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            string state = Base64Url.EncodeToString(new AesGcmSecretCipher(MicrosoftAuthHarness.StateKey).Encrypt(Encoding.UTF8.GetBytes(plaintext)));

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync("any-code", state, harness.CreateBrowser());

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidState, result.FailureReason);
            Assert.AreEqual(0, harness.IdentityProvider.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_StateOlderThanItsLifetime_IsRefusedAsExpiredState()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create(MicrosoftAuthHarness.CreateOptions(stateLifetime: TimeSpan.FromMinutes(10)));
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);
            string code = harness.IdentityProvider.IssueCode(begun.AuthorizationUrl);
            harness.Clock.Advance(TimeSpan.FromMinutes(10));

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync(code, begun.State, browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.ExpiredState, result.FailureReason);
            Assert.AreEqual(0, harness.IdentityProvider.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_StateJustInsideItsLifetime_Completes()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create(MicrosoftAuthHarness.CreateOptions(stateLifetime: TimeSpan.FromMinutes(10)));
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);
            harness.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
            string code = harness.IdentityProvider.IssueCode(begun.AuthorizationUrl);

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync(code, begun.State, browser);

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_FromABrowserWithoutTheBindingCookie_IsRefusedAsBindingCookieMissing()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), harness.CreateBrowser());
            string code = harness.IdentityProvider.IssueCode(begun.AuthorizationUrl);

            // Login CSRF: the starter's code and state, finished in the victim's browser.
            MicrosoftAuthCompletionResult result = await harness.CompleteAsync(code, begun.State, harness.CreateBrowser());

            Assert.AreEqual(MicrosoftAuthFailureReason.BindingCookieMissing, result.FailureReason);
            Assert.AreEqual(0, harness.IdentityProvider.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_WithAnotherValueInTheBindingCookie_IsRefusedAsBindingCookieMismatch()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);
            string cookieName = browser.Cookies.Keys.Single();
            browser.Set(cookieName, "planted-value");

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync(harness.IdentityProvider.IssueCode(begun.AuthorizationUrl), begun.State, browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.BindingCookieMismatch, result.FailureReason);
            Assert.AreEqual(0, harness.IdentityProvider.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_WithAnotherFlowsCookieValue_IsRefusedAsBindingCookieMismatch()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult first = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);
            string firstCookie = browser.Cookies.Keys.Single();
            harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);
            string secondCookie = browser.Cookies.Keys.Single(name => name != firstCookie);
            browser.Set(firstCookie, browser.Cookies[secondCookie]);

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync(harness.IdentityProvider.IssueCode(first.AuthorizationUrl), first.State, browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.BindingCookieMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_CodeMicrosoftRefuses_IsRefusedAsCodeRedemptionFailed()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync("code-microsoft-never-issued", begun.State, browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.CodeRedemptionFailed, result.FailureReason);
            Assert.AreEqual(1, harness.IdentityProvider.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_EmptyCode_IsRefusedAsCodeRedemptionFailedWithoutCallingMicrosoft()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            MicrosoftAuthBeginResult begun = harness.Begin(MicrosoftAuthHarness.CreateRequest(), browser);

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync("", begun.State, browser);

            Assert.AreEqual(MicrosoftAuthFailureReason.CodeRedemptionFailed, result.FailureReason);
            Assert.AreEqual(0, harness.IdentityProvider.TokenRequests.Count);
        }

        [TestMethod]
        public async Task Complete_TokenEndpointUnauthorizedForTheClient_IsRefusedAsCodeRedemptionFailed()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            harness.IdentityProvider.TokenEndpointStatusOverride = HttpStatusCode.Unauthorized;

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest());

            Assert.AreEqual(MicrosoftAuthFailureReason.CodeRedemptionFailed, result.FailureReason);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        [DataRow(HttpStatusCode.TooManyRequests)]
        public async Task Complete_TokenEndpointServerErrorOrThrottle_ThrowsInsteadOfReportingARefusal(HttpStatusCode status)
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            harness.IdentityProvider.TokenEndpointStatusOverride = status;

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => harness.SignInAsync(MicrosoftAuthHarness.CreateRequest()));
        }

        [TestMethod]
        [DataRow("{\"access_token\":\"only-an-access-token\"}")]
        [DataRow("{\"id_token\":\"\"}")]
        [DataRow("{\"id_token\":42}")]
        [DataRow("[\"not\",\"an\",\"object\"]")]
        [DataRow("<html>not json</html>")]
        public async Task Complete_TokenEndpointSuccessWithoutAUsableIdToken_IsRefusedAsCodeRedemptionFailed(string body)
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            harness.IdentityProvider.TokenEndpointSuccessBodyOverride = body;

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest());

            Assert.AreEqual(MicrosoftAuthFailureReason.CodeRedemptionFailed, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_SigningKeysUnavailableOnTheFirstFetch_ThrowsInvalidOperationException()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            harness.IdentityProvider.MetadataStatusOverride = HttpStatusCode.ServiceUnavailable;

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.SignInAsync(MicrosoftAuthHarness.CreateRequest()));
        }

        [TestMethod]
        public async Task Complete_SigningKeysUnavailableOnTheForcedFetch_ThrowsInvalidOperationException()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest());
            harness.IdentityProvider.MetadataStatusOverride = HttpStatusCode.ServiceUnavailable;

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(),
                token => token.SigningKey = FakeMicrosoftIdentityProvider.CreateKey("unknown-to-the-cache")));
        }

        [TestMethod]
        public async Task Complete_MetadataNamingAnHttpKeySet_RefusesToFetchItAndThrows()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            harness.IdentityProvider.JwksUri = "http://keys.fake.test/organizations/discovery/v2.0/keys";

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => harness.SignInAsync(MicrosoftAuthHarness.CreateRequest()));

            Assert.AreEqual(0, harness.IdentityProvider.KeysRequestCount);
            Assert.IsTrue(ContainsInChain<ArgumentException>(exception), exception.ToString());
        }

        [TestMethod]
        public async Task Complete_TokenSignedByAnUnpublishedKey_IsRefusedAsInvalidSignature()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(),
                token => token.SigningKey = FakeMicrosoftIdentityProvider.CreateKey("unpublished-key"));

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidSignature, result.FailureReason);
            Assert.IsNull(result.AttemptedSubject);
        }

        [TestMethod]
        public async Task Complete_TokenSignedByAnotherKeyUnderThePublishedKeyId_IsRefusedAsInvalidSignature()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(),
                token => token.SigningKey = FakeMicrosoftIdentityProvider.CreateKey(harness.IdentityProvider.SigningKey.KeyId));

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidSignature, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenForAnotherAudience_IsRefusedAsInvalidAudience()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(),
                token => token.Claims["aud"] = "another-application");

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidAudience, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_IssuerOfAnotherTenantThanTheTokensTid_IsRefusedAsInvalidIssuer()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(),
                token => token.Claims["iss"] = $"{FakeMicrosoftIdentityProvider.Authority}/{FakeMicrosoftIdentityProvider.OtherTenantId}/v2.0");

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidIssuer, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_IssuerOnAnotherAuthority_IsRefusedAsInvalidIssuer()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(),
                token => token.Claims["iss"] = $"https://login.elsewhere.test/{FakeMicrosoftIdentityProvider.TenantId}/v2.0");

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidIssuer, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenWithoutTid_IsRefusedAsInvalidIssuer()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token => token.Claims.Remove("tid"));

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidIssuer, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_ExpiredToken_IsRefusedAsTokenExpired()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            long now = harness.Clock.GetUtcNow().ToUnixTimeSeconds();

            // Expired by more than the five minutes of clock skew allowed.
            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token =>
            {
                token.Claims["iat"] = now - 3600;
                token.Claims["nbf"] = now - 3600;
                token.Claims["exp"] = now - 301;
            });

            Assert.AreEqual(MicrosoftAuthFailureReason.TokenExpired, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenNotValidYet_IsRefusedAsInvalidToken()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            long now = harness.Clock.GetUtcNow().ToUnixTimeSeconds();

            // Valid from beyond the five minutes of clock skew allowed.
            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token =>
            {
                token.Claims["nbf"] = now + 301;
                token.Claims["exp"] = now + 3600;
            });

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidToken, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenSignedWithThePublishedKeyButNotRs256_IsRefusedAsInvalidSignature()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            // The key is genuine and the RS384 signature verifies with it; only the algorithm allowlist refuses it.
            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(),
                token => token.Algorithm = SecurityAlgorithms.RsaSha384);

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidSignature, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TidThatIsNotAGuidWithAMatchingIssuer_IsRefusedAsInvalidToken()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token =>
            {
                token.Claims["tid"] = "contoso";
                token.Claims["iss"] = $"{FakeMicrosoftIdentityProvider.Authority}/contoso/v2.0";
            });

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidToken, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenLifetime_IsCheckedOnTheRegisteredClock()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            long now = harness.Clock.GetUtcNow().ToUnixTimeSeconds();

            // The fake clock stands in 2030, so on the wall clock this token is not valid yet (its nbf is years
            // ahead). Only a check against the registered clock accepts it.
            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token => token.Claims["exp"] = now + 60);

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_TokenWithAnotherNonce_IsRefusedAsNonceMismatch()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token => token.Claims["nonce"] = "replayed-nonce");

            Assert.AreEqual(MicrosoftAuthFailureReason.NonceMismatch, result.FailureReason);
            Assert.IsNull(result.AttemptedSubject);
        }

        [TestMethod]
        public async Task Complete_TokenWithoutNonce_IsRefusedAsNonceMismatch()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token => token.Claims.Remove("nonce"));

            Assert.AreEqual(MicrosoftAuthFailureReason.NonceMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenWithoutOid_IsRefusedAsInvalidToken()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token => token.Claims.Remove("oid"));

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidToken, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TokenWithANonNumericAuthTime_IsRefusedAsInvalidToken()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token => token.Claims["auth_time"] = "yesterday");

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidToken, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_PersonalAccount_IsRefusedAsPersonalAccountNamingTheAccount()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            string personal = FakeMicrosoftIdentityProvider.PersonalAccountsTenantId;

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token =>
            {
                token.Claims["tid"] = personal;
                token.Claims["iss"] = $"{FakeMicrosoftIdentityProvider.Authority}/{personal}/v2.0";
            });

            Assert.AreEqual(MicrosoftAuthFailureReason.PersonalAccount, result.FailureReason);
            Assert.AreEqual($"{personal}:{FakeMicrosoftIdentityProvider.ObjectId}", result.AttemptedSubject);
            Assert.IsNull(result.UserInfo);
            Assert.IsNull(result.CallerBinding);
        }

        [TestMethod]
        public async Task Complete_PersonalAccountWithAnUppercaseTidAndAnEmptyAllowlist_IsStillRefused()
        {
            // The allowlist cannot hold the personal tenant (registration refuses it), so an empty allowlist, which
            // admits every tenant, is the widest one there is. The uppercase tid shows the comparison is by value.
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create(MicrosoftAuthHarness.CreateOptions(allowedTenantIds: []));
            string personal = FakeMicrosoftIdentityProvider.PersonalAccountsTenantId;

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token =>
            {
                token.Claims["tid"] = personal.ToUpperInvariant();
                token.Claims["iss"] = $"{FakeMicrosoftIdentityProvider.Authority}/{personal.ToUpperInvariant()}/v2.0";
            });

            Assert.AreEqual(MicrosoftAuthFailureReason.PersonalAccount, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_TenantOutsideTheAllowlist_IsRefusedAsTenantNotAllowed()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create(
                MicrosoftAuthHarness.CreateOptions(allowedTenantIds: [FakeMicrosoftIdentityProvider.OtherTenant]));

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest());

            Assert.AreEqual(MicrosoftAuthFailureReason.TenantNotAllowed, result.FailureReason);
            Assert.AreEqual(Subject, result.AttemptedSubject);
        }

        [TestMethod]
        public async Task Complete_AllowedTenantWithAnUppercaseTid_CompletesAndReportsTheCanonicalSubject()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create(MicrosoftAuthHarness.CreateOptions(
                allowedTenantIds: [FakeMicrosoftIdentityProvider.OtherTenant, FakeMicrosoftIdentityProvider.Tenant]));
            string upper = FakeMicrosoftIdentityProvider.TenantId.ToUpperInvariant();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token =>
            {
                token.Claims["tid"] = upper;
                token.Claims["iss"] = $"{FakeMicrosoftIdentityProvider.Authority}/{upper}/v2.0";
            });

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            Assert.AreEqual(Subject, result.AttemptedSubject);
        }

        [TestMethod]
        public async Task Complete_TokenFromAnotherTenantThanTheFlowRequired_IsRefusedAsTenantMismatch()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            string other = FakeMicrosoftIdentityProvider.OtherTenantId;

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(tenantId: FakeMicrosoftIdentityProvider.Tenant), token =>
            {
                token.Claims["tid"] = other;
                token.Claims["iss"] = $"{FakeMicrosoftIdentityProvider.Authority}/{other}/v2.0";
            });

            Assert.AreEqual(MicrosoftAuthFailureReason.TenantMismatch, result.FailureReason);
            Assert.AreEqual($"{other}:{FakeMicrosoftIdentityProvider.ObjectId}", result.AttemptedSubject);
        }

        [TestMethod]
        public async Task Complete_RequiredAuthenticationContextMissingFromAcrs_IsRefusedAsMissingAuthenticationContext()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(
                MicrosoftAuthHarness.CreateRequest(requiredAuthenticationContextId: "c2"), token => token.Claims["acrs"] = new[] { "c1", "c3" });

            Assert.AreEqual(MicrosoftAuthFailureReason.MissingAuthenticationContext, result.FailureReason);
            Assert.AreEqual(Subject, result.AttemptedSubject);
        }

        [TestMethod]
        public async Task Complete_RequiredAuthenticationContextAndNoAcrs_IsRefusedAsMissingAuthenticationContext()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(
                MicrosoftAuthHarness.CreateRequest(requiredAuthenticationContextId: "c1"), token => token.Claims.Remove("acrs"));

            Assert.AreEqual(MicrosoftAuthFailureReason.MissingAuthenticationContext, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_RequiredAuthenticationContextInAcrs_Completes()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(
                MicrosoftAuthHarness.CreateRequest(requiredAuthenticationContextId: "c2"), token => token.Claims["acrs"] = new[] { "c1", "c2" });

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_MaxAgeAndNoAuthTime_IsRefusedAsMissingAuthTime()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(
                MicrosoftAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromMinutes(5)), token => token.Claims.Remove("auth_time"));

            Assert.AreEqual(MicrosoftAuthFailureReason.MissingAuthTime, result.FailureReason);
            Assert.AreEqual(Subject, result.AttemptedSubject);
        }

        [TestMethod]
        public async Task Complete_MaxAgeAndAnAuthTimeOlderThanIt_IsRefusedAsStaleAuthTime()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            long now = harness.Clock.GetUtcNow().ToUnixTimeSeconds();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(
                MicrosoftAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromMinutes(5)), token => token.Claims["auth_time"] = now - 301);

            Assert.AreEqual(MicrosoftAuthFailureReason.StaleAuthTime, result.FailureReason);
        }

        [TestMethod]
        public async Task Complete_MaxAgeAndAnAuthTimeExactlyAtIt_Completes()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            long now = harness.Clock.GetUtcNow().ToUnixTimeSeconds();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(
                MicrosoftAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromMinutes(5)), token => token.Claims["auth_time"] = now - 300);

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_FractionalMaxAge_AppliesTheWholeSecondsBeginSent()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            long now = harness.Clock.GetUtcNow().ToUnixTimeSeconds();
            MicrosoftAuthBeginRequest request = MicrosoftAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromSeconds(90.5));

            MicrosoftAuthCompletionResult atTheLimit = await harness.SignInAsync(request, token => token.Claims["auth_time"] = now - 90);
            MicrosoftAuthCompletionResult pastTheLimit = await harness.SignInAsync(request, token => token.Claims["auth_time"] = now - 91);

            Assert.IsTrue(atTheLimit.Success, atTheLimit.FailureReason?.ToString());
            Assert.AreEqual(MicrosoftAuthFailureReason.StaleAuthTime, pastTheLimit.FailureReason);
        }

        [TestMethod]
        public async Task Complete_NoMaxAgeAndAnOldAuthTime_Completes()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            long now = harness.Clock.GetUtcNow().ToUnixTimeSeconds();

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token => token.Claims["auth_time"] = now - 86400);

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_SeveralFlows_FetchTheSigningKeysOnce()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest());
            await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest());

            Assert.AreEqual(1, harness.IdentityProvider.MetadataRequestCount);
            Assert.AreEqual(1, harness.IdentityProvider.KeysRequestCount);
        }

        [TestMethod]
        public async Task Complete_TokenSignedByAKeyPublishedAfterTheCachedSet_FetchesTheKeysAfreshAndCompletes()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest());
            Assert.AreEqual(1, harness.IdentityProvider.KeysRequestCount);
            RsaSecurityKey rotatedKey = FakeMicrosoftIdentityProvider.CreateKey("rotated-key");
            harness.IdentityProvider.PublishedKeys = [harness.IdentityProvider.SigningKey, rotatedKey];

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(), token => token.SigningKey = rotatedKey);

            // Only the rotated key's set verifies this token, so success proves the keys were fetched afresh.
            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Complete_KeyInNeitherTheCachedNorTheFreshSet_IsRefusedAsInvalidSignatureWithoutARetryLoop()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest());
            int metadataRequestsBefore = harness.IdentityProvider.MetadataRequestCount;

            MicrosoftAuthCompletionResult result = await harness.SignInAsync(MicrosoftAuthHarness.CreateRequest(),
                token => token.SigningKey = FakeMicrosoftIdentityProvider.CreateKey("never-published"));

            Assert.AreEqual(MicrosoftAuthFailureReason.InvalidSignature, result.FailureReason);
            // One forced fetch, plus at most the cache's own background refresh: no retry loop.
            int metadataRequests = harness.IdentityProvider.MetadataRequestCount - metadataRequestsBefore;
            Assert.IsTrue(metadataRequests >= 1 && metadataRequests <= 2, $"{metadataRequests} metadata requests");
        }

        private static bool ContainsInChain<TException>(Exception exception) where TException : Exception
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is TException)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
