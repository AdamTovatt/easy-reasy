using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth.Microsoft.Tests
{
    [TestClass]
    public class MicrosoftAuthServiceBeginTests
    {
        [TestMethod]
        public async Task Begin_DefaultRequest_SendsCodeFlowWithPkceInQueryModeOnTheOrganizationsSegment()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(), harness.CreateBrowser());

            Uri url = new Uri(result.AuthorizationUrl);
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(url.Query);
            Assert.AreEqual($"{FakeMicrosoftIdentityProvider.Authority}/organizations/oauth2/v2.0/authorize", url.GetLeftPart(UriPartial.Path));
            Assert.AreEqual(FakeMicrosoftIdentityProvider.ClientId, query["client_id"].ToString());
            Assert.AreEqual("code", query["response_type"].ToString());
            Assert.AreEqual("query", query["response_mode"].ToString());
            Assert.AreEqual("openid profile", query["scope"].ToString());
            Assert.AreEqual(MicrosoftAuthHarness.SigningRedirectUri, query["redirect_uri"].ToString());
            Assert.AreEqual(result.State, query["state"].ToString());
            Assert.AreEqual("S256", query["code_challenge_method"].ToString());
            // A SHA-256 digest (32 bytes) as unpadded base64url is 43 characters. That the challenge matches the
            // verifier is proved at redemption, where the fake identity provider refuses a mismatched one.
            Assert.AreEqual(43, query["code_challenge"].ToString().Length);
            Assert.IsFalse(string.IsNullOrEmpty(query["nonce"].ToString()));
        }

        [TestMethod]
        public async Task Begin_DefaultRequest_SendsNoPromptLoginHintMaxAgeOrClaims()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(), harness.CreateBrowser());

            Dictionary<string, StringValues> query = ParseQuery(result);
            Assert.IsFalse(query.ContainsKey("prompt"));
            Assert.IsFalse(query.ContainsKey("login_hint"));
            Assert.IsFalse(query.ContainsKey("max_age"));
            Assert.IsFalse(query.ContainsKey("claims"));
        }

        [TestMethod]
        [DataRow("22222222-bbbb-4bbb-8bbb-222222222222")]
        [DataRow("{22222222-BBBB-4BBB-8BBB-222222222222}")]
        [DataRow("(22222222-bbbb-4bbb-8bbb-222222222222)")]
        [DataRow("22222222bbbb4bbb8bbb222222222222")]
        [DataRow(" 22222222-bbbb-4bbb-8bbb-222222222222 ")]
        public async Task Begin_WithTenantIdParsedFromAnyGuidForm_SendsTheCanonicalFormAsTheAuthoritySegment(string tenantText)
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(tenantId: Guid.Parse(tenantText)), harness.CreateBrowser());

            Assert.AreEqual(
                $"{FakeMicrosoftIdentityProvider.Authority}/{FakeMicrosoftIdentityProvider.TenantId}/oauth2/v2.0/authorize",
                new Uri(result.AuthorizationUrl).GetLeftPart(UriPartial.Path));
        }

        [TestMethod]
        public async Task Begin_WithLoginHintAndMaxAge_SendsThem()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            MicrosoftAuthBeginRequest request = MicrosoftAuthHarness.CreateRequest(
                loginHint: "ada@contoso.example", maxAuthenticationAge: TimeSpan.FromMinutes(5));

            Dictionary<string, StringValues> query = ParseQuery(await harness.BeginAsync(request, harness.CreateBrowser()));

            Assert.AreEqual("ada@contoso.example", query["login_hint"].ToString());
            Assert.AreEqual("300", query["max_age"].ToString());
        }

        [TestMethod]
        public async Task Begin_WithAFractionalMaxAge_SendsTheWholeSeconds()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromSeconds(90.5)), harness.CreateBrowser());

            Assert.AreEqual("90", ParseQuery(result)["max_age"].ToString());
        }

        [TestMethod]
        public async Task Begin_WithEachPrompt_SendsItsProtocolValue()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            Dictionary<OpenIdConnectPrompt, string> expected = new Dictionary<OpenIdConnectPrompt, string>
            {
                [OpenIdConnectPrompt.Login] = "login",
                [OpenIdConnectPrompt.None] = "none",
                [OpenIdConnectPrompt.Consent] = "consent",
                [OpenIdConnectPrompt.SelectAccount] = "select_account",
            };
            Assert.AreEqual(Enum.GetValues<OpenIdConnectPrompt>().Length, expected.Count);

            foreach (KeyValuePair<OpenIdConnectPrompt, string> pair in expected)
            {
                MicrosoftAuthBeginRequest request = MicrosoftAuthHarness.CreateRequest(prompt: pair.Key);

                Assert.AreEqual(pair.Value, ParseQuery(await harness.BeginAsync(request, harness.CreateBrowser()))["prompt"].ToString(), pair.Key.ToString());
            }
        }

        [TestMethod]
        public async Task Begin_WithRequiredAuthenticationContext_SendsEssentialAcrsClaimsRequest()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(requiredAuthenticationContextId: "c7"), harness.CreateBrowser());

            Assert.AreEqual("{\"id_token\":{\"acrs\":{\"essential\":true,\"value\":\"c7\"}}}", ParseQuery(result)["claims"].ToString());
        }

        [TestMethod]
        public async Task Begin_WithCallerNonce_SendsThatNonce()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(nonce: "record-hash-abc"), harness.CreateBrowser());

            Assert.AreEqual("record-hash-abc", ParseQuery(result)["nonce"].ToString());
        }

        [TestMethod]
        public async Task Begin_WithoutCallerNonce_GeneratesADifferentNonceEachTime()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            string first = ParseQuery(await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(), harness.CreateBrowser()))["nonce"].ToString();
            string second = ParseQuery(await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(), harness.CreateBrowser()))["nonce"].ToString();

            Assert.AreNotEqual(first, second);
        }

        [TestMethod]
        public async Task Begin_StateLifetime_SetsTheSharedFlowsBindingCookieForThatLong()
        {
            // The cookie's attributes are EasyReasy.Auth's FlowBindingCookie's own; this package decides only its lifetime.
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create(MicrosoftAuthHarness.CreateOptions(stateLifetime: TimeSpan.FromMinutes(7)));
            DefaultHttpContext context = new DefaultHttpContext();

            await harness.Service.BeginAsync(MicrosoftAuthHarness.CreateRequest(), context);

            SetCookieHeaderValue cookie = SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie.Select(value => value ?? "").ToList()).Single();
            StringAssert.StartsWith(cookie.Name.ToString(), OpenIdConnectCodeFlow.BindingCookieNamePrefix);
            Assert.AreEqual(TimeSpan.FromMinutes(7), cookie.MaxAge);
        }

        [TestMethod]
        public async Task Begin_TwoFlowsInOneBrowser_SetTwoCookiesUnderDifferentNames()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();

            await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(), browser);
            await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(), browser);

            Assert.AreEqual(2, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Begin_StateIsOpaque_TheCodeVerifierAndCallerBindingDoNotAppearInTheUrl()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(), browser);

            await harness.CompleteAsync(harness.IdentityProvider.IssueCode(begun.AuthorizationUrl), begun.State, browser);

            string verifier = harness.IdentityProvider.TokenRequests.Single()["code_verifier"];
            Assert.IsFalse(begun.AuthorizationUrl.Contains(verifier, StringComparison.Ordinal));
            Assert.IsFalse(Uri.UnescapeDataString(begun.AuthorizationUrl).Contains(MicrosoftAuthHarness.CallerBinding, StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task Begin_RedirectUriNotOnTheAllowlist_ThrowsAndSetsNoCookie()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            DefaultHttpContext context = new DefaultHttpContext();

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(redirectUri: "https://evil.example/callback"), context));

            StringAssert.Contains(exception.Message, "https://evil.example/callback");
            Assert.AreEqual(0, context.Response.Headers.SetCookie.Count);
        }

        [TestMethod]
        public async Task Begin_NullRedirectUri_ThrowsAndSetsNoCookie()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            DefaultHttpContext context = new DefaultHttpContext();
            string? missing = null;
            MicrosoftAuthBeginRequest request = new MicrosoftAuthBeginRequest { RedirectUri = missing!, CallerBinding = MicrosoftAuthHarness.CallerBinding };

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(request, context));

            StringAssert.Contains(exception.Message, nameof(MicrosoftAuthBeginRequest.RedirectUri));
            Assert.AreEqual(0, context.Response.Headers.SetCookie.Count);
        }

        [TestMethod]
        public async Task Begin_NullCallerBinding_ThrowsAndSetsNoCookie()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            DefaultHttpContext context = new DefaultHttpContext();
            string? missing = null;
            MicrosoftAuthBeginRequest request = new MicrosoftAuthBeginRequest { RedirectUri = MicrosoftAuthHarness.SigningRedirectUri, CallerBinding = missing! };

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(request, context));

            StringAssert.Contains(exception.Message, nameof(MicrosoftAuthBeginRequest.CallerBinding));
            Assert.AreEqual(0, context.Response.Headers.SetCookie.Count);
        }

        [TestMethod]
        public async Task Begin_RedirectUriDifferingOnlyInCase_Throws()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(redirectUri: MicrosoftAuthHarness.SigningRedirectUri.ToUpperInvariant()), new DefaultHttpContext()));
        }

        [TestMethod]
        public async Task Begin_PersonalAccountsTenant_Throws()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(tenantId: FakeMicrosoftIdentityProvider.PersonalAccountsTenant), new DefaultHttpContext()));
        }

        [TestMethod]
        public async Task Begin_MaxAgeUnderOneSecond_Throws()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(maxAuthenticationAge: TimeSpan.FromMilliseconds(500)), new DefaultHttpContext()));
        }

        [TestMethod]
        public async Task Begin_BlankNonce_Throws()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(nonce: " "), new DefaultHttpContext()));
        }

        [TestMethod]
        public async Task Begin_BlankAuthenticationContextId_Throws()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(
                MicrosoftAuthHarness.CreateRequest(requiredAuthenticationContextId: ""), new DefaultHttpContext()));
        }

        [TestMethod]
        public async Task Begin_UndefinedPrompt_ThrowsAndSetsNoCookie()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            DefaultHttpContext context = new DefaultHttpContext();
            MicrosoftAuthBeginRequest request = MicrosoftAuthHarness.CreateRequest(prompt: (OpenIdConnectPrompt)42);

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(request, context));
            Assert.AreEqual(0, context.Response.Headers.SetCookie.Count);
        }

        [TestMethod]
        public async Task Begin_BothEntraProblems_ReportsThemInOneException()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            MicrosoftAuthBeginRequest request = MicrosoftAuthHarness.CreateRequest(
                tenantId: FakeMicrosoftIdentityProvider.PersonalAccountsTenant,
                requiredAuthenticationContextId: " ");

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(request, new DefaultHttpContext()));

            StringAssert.Contains(exception.Message, "personal");
            StringAssert.Contains(exception.Message, nameof(MicrosoftAuthBeginRequest.RequiredAuthenticationContextId));
        }

        [TestMethod]
        public async Task Begin_SeveralRequestProblems_ReportsEveryOneInOneException()
        {
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create();
            MicrosoftAuthBeginRequest request = MicrosoftAuthHarness.CreateRequest(
                redirectUri: "https://evil.example/callback",
                maxAuthenticationAge: TimeSpan.Zero,
                nonce: " ");

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.Service.BeginAsync(request, new DefaultHttpContext()));

            StringAssert.Contains(exception.Message, "evil.example");
            StringAssert.Contains(exception.Message, nameof(MicrosoftAuthBeginRequest.MaxAuthenticationAge));
            StringAssert.Contains(exception.Message, nameof(MicrosoftAuthBeginRequest.Nonce));
        }

        private static Dictionary<string, StringValues> ParseQuery(OpenIdConnectAuthBeginResult result)
        {
            return QueryHelpers.ParseQuery(new Uri(result.AuthorizationUrl).Query);
        }
    }
}
