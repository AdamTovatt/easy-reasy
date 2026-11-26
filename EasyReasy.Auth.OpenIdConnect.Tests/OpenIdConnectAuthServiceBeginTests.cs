using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Net;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// What Begin sends the provider, the binding cookie it sets, and what it refuses before anything is issued.
    /// </summary>
    [TestClass]
    public class OpenIdConnectAuthServiceBeginTests
    {
        [TestMethod]
        public async Task Begin_DefaultRequest_SendsTheCodeFlowWithPkceInQueryModeToTheDiscoveredAuthorizeEndpoint()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), harness.CreateBrowser());
            Dictionary<string, StringValues> query = OpenIdConnectAuthHarness.ParseQuery(result.AuthorizationUrl);

            StringAssert.StartsWith(result.AuthorizationUrl, harness.Primary.AuthorizationEndpoint + "?");
            Assert.AreEqual(FakeOpenIdConnectIssuer.ClientId, query["client_id"].ToString());
            Assert.AreEqual("code", query["response_type"].ToString());
            Assert.AreEqual(OpenIdConnectAuthHarness.SigningRedirectUri(OpenIdConnectAuthHarness.PrimaryKey), query["redirect_uri"].ToString());
            Assert.AreEqual("query", query["response_mode"].ToString());
            Assert.AreEqual(OpenIdConnectProvider.DefaultScope, query["scope"].ToString());
            Assert.AreEqual(result.State, query["state"].ToString());
            Assert.AreEqual("S256", query["code_challenge_method"].ToString());
            Assert.AreEqual(43, query["code_challenge"].ToString().Length);
            Assert.IsFalse(string.IsNullOrEmpty(query["nonce"].ToString()));
        }

        [TestMethod]
        public async Task Begin_DefaultRequest_SendsNoPromptLoginHintMaxAgeOrClaims()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), harness.CreateBrowser());
            Dictionary<string, StringValues> query = OpenIdConnectAuthHarness.ParseQuery(result.AuthorizationUrl);

            Assert.IsFalse(query.ContainsKey("prompt"));
            Assert.IsFalse(query.ContainsKey("login_hint"));
            Assert.IsFalse(query.ContainsKey("max_age"));
            Assert.IsFalse(query.ContainsKey("claims"));
        }

        [TestMethod]
        public async Task Begin_ProviderWithItsOwnScope_SendsThatScope()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.ChangeProvider(OpenIdConnectAuthHarness.PrimaryKey, scope: "openid groups");

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), harness.CreateBrowser());

            Assert.AreEqual("openid groups", OpenIdConnectAuthHarness.ParseQuery(result.AuthorizationUrl)["scope"].ToString());
        }

        [TestMethod]
        [DataRow(OpenIdConnectPrompt.Login, "login")]
        [DataRow(OpenIdConnectPrompt.None, "none")]
        [DataRow(OpenIdConnectPrompt.Consent, "consent")]
        [DataRow(OpenIdConnectPrompt.SelectAccount, "select_account")]
        public async Task Begin_WithEachPrompt_SendsItsProtocolValue(OpenIdConnectPrompt prompt, string expected)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(prompt: prompt), harness.CreateBrowser());

            Assert.AreEqual(expected, OpenIdConnectAuthHarness.ParseQuery(result.AuthorizationUrl)["prompt"].ToString());
        }

        [TestMethod]
        public async Task Begin_WithLoginHintAndAFractionalMaxAge_SendsThemAndAsksForAuthTimeAsEssential()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(loginHint: "ada@example.test", maxAuthenticationAge: TimeSpan.FromSeconds(300.9)),
                harness.CreateBrowser());
            Dictionary<string, StringValues> query = OpenIdConnectAuthHarness.ParseQuery(result.AuthorizationUrl);

            Assert.AreEqual("ada@example.test", query["login_hint"].ToString());
            Assert.AreEqual("300", query["max_age"].ToString());
            Assert.AreEqual("{\"id_token\":{\"auth_time\":{\"essential\":true}}}", query["claims"].ToString());
        }

        [TestMethod]
        public async Task Begin_WithAcceptedAcrAndAmrValues_AsksForAcrWithTheValuesAndAmrAsEssential()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(acceptedAcrValues: ["urn:mfa", "urn:hwk"], acceptedAmrValues: ["otp"]),
                harness.CreateBrowser());

            Assert.AreEqual(
                "{\"id_token\":{\"acr\":{\"essential\":true,\"values\":[\"urn:mfa\",\"urn:hwk\"]},\"amr\":{\"essential\":true}}}",
                OpenIdConnectAuthHarness.ParseQuery(result.AuthorizationUrl)["claims"].ToString());
        }

        [TestMethod]
        public async Task Begin_WithRequiredClaimValues_AsksForEachAsEssentialWithItsValueBesideTheOtherRules()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(
                    maxAuthenticationAge: TimeSpan.FromMinutes(5),
                    requiredClaimValues: new Dictionary<string, string> { ["acrs"] = "c1" }),
                harness.CreateBrowser());

            Assert.AreEqual(
                "{\"id_token\":{\"auth_time\":{\"essential\":true},\"acrs\":{\"essential\":true,\"value\":\"c1\"}}}",
                OpenIdConnectAuthHarness.ParseQuery(result.AuthorizationUrl)["claims"].ToString());
        }

        [TestMethod]
        [DataRow("auth_time")]
        [DataRow("acr")]
        [DataRow("amr")]
        public async Task Begin_RequiredClaimNamingAClaimTheOtherRulesAskFor_ThrowsAndSetsNoCookie(string claim)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(requiredClaimValues: new Dictionary<string, string> { [claim] = "x" }),
                browser));

            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        [DataRow(" ", "x")]
        [DataRow("acrs", " ")]
        public async Task Begin_RequiredClaimWithABlankNameOrValue_ThrowsAndSetsNoCookie(string name, string value)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(requiredClaimValues: new Dictionary<string, string> { [name] = value }),
                browser));

            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Begin_WithCallerNonce_SendsThatNonce()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(nonce: "record-hash"), harness.CreateBrowser());

            Assert.AreEqual("record-hash", OpenIdConnectAuthHarness.ParseQuery(result.AuthorizationUrl)["nonce"].ToString());
        }

        [TestMethod]
        public async Task Begin_WithoutCallerNonce_GeneratesADifferentNonceEachTime()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();

            OpenIdConnectAuthBeginResult first = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            OpenIdConnectAuthBeginResult second = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            Assert.AreNotEqual(OpenIdConnectAuthHarness.ParseQuery(first.AuthorizationUrl)["nonce"].ToString(), OpenIdConnectAuthHarness.ParseQuery(second.AuthorizationUrl)["nonce"].ToString());
        }

        [TestMethod]
        public async Task Begin_SetsOneHostPrefixedHttpOnlySecureLaxCookieThatExpiresWithTheState()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            DefaultHttpContext context = harness.CreateContext(harness.CreateBrowser());

            await harness.Service.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), context);

            SetCookieHeaderValue cookie = SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie.Select(value => value ?? "").ToList()).Single();
            // A literal, not the package's constant: the __Host- prefix is the security property this test pins.
            StringAssert.StartsWith(cookie.Name.ToString(), "__Host-");
            Assert.IsTrue(cookie.HttpOnly);
            Assert.IsTrue(cookie.Secure);
            Assert.AreEqual(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
            Assert.AreEqual("/", cookie.Path.ToString());
            Assert.IsTrue(StringSegment.IsNullOrEmpty(cookie.Domain));
            Assert.AreEqual(TimeSpan.FromMinutes(10), cookie.MaxAge);
        }

        [TestMethod]
        public async Task Begin_FlowsUpToTheLimitInOneBrowser_KeepEveryCookie()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();

            for (int flow = 0; flow < FlowBindingCookie.MaxPendingFlows; flow++)
            {
                await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            }

            Assert.AreEqual(FlowBindingCookie.MaxPendingFlows, OpenIdConnectAuthHarness.CountBindingCookies(browser));
        }

        [TestMethod]
        public async Task Begin_OneFlowOverTheLimit_ClearsThePendingCookiesAndKeepsItsOwnAndUnrelatedOnes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            browser.Set("unrelated", "kept");
            await BeginUpToTheLimitAsync(harness, browser);

            await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            Assert.AreEqual(1, OpenIdConnectAuthHarness.CountBindingCookies(browser));
            Assert.AreEqual("kept", browser.Cookies["unrelated"]);
        }

        [TestMethod]
        public async Task Begin_OneFlowOverTheLimit_ThatFlowCompletes()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            await BeginUpToTheLimitAsync(harness, browser);
            OpenIdConnectAuthBeginResult latest = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            OpenIdConnectAuthCompletionResult completed = await harness.CompleteBegunAsync(latest, browser);

            Assert.IsTrue(completed.Success, completed.FailureReason?.ToString());
        }

        [TestMethod]
        public async Task Begin_OneFlowOverTheLimit_AFlowWhoseCookieWasClearedIsRefused()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            IReadOnlyList<OpenIdConnectAuthBeginResult> pending = await BeginUpToTheLimitAsync(harness, browser);
            await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            OpenIdConnectAuthCompletionResult abandoned = await harness.CompleteBegunAsync(pending[0], browser);

            Assert.AreEqual(OpenIdConnectAuthFailureReason.BindingCookieMissing, abandoned.FailureReason);
        }

        [TestMethod]
        public async Task Begin_StateIsOpaque_TheCallerBindingProviderKeyAndClientIdDoNotAppearInTheUrl()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), harness.CreateBrowser());

            Assert.IsFalse(result.AuthorizationUrl.Contains(OpenIdConnectAuthHarness.CallerBinding, StringComparison.Ordinal));
            Assert.IsFalse(result.State.Contains(OpenIdConnectAuthHarness.PrimaryKey, StringComparison.Ordinal));
            Assert.IsFalse(result.State.Contains(FakeOpenIdConnectIssuer.ClientId, StringComparison.Ordinal));
        }

        [TestMethod]
        [DataRow("nobody")]
        [DataRow(" ")]
        public async Task Begin_ProviderKeyResolvingToNoProvider_ReturnsNullWithoutACookieOrAFetch(string providerKey)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();

            OpenIdConnectAuthBeginResult? result = await harness.TryBeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(providerKey: providerKey, redirectUri: "https://app.example/x"),
                browser);

            Assert.IsNull(result);
            Assert.AreEqual(0, browser.Cookies.Count);
            Assert.AreEqual(0, harness.Network.RequestCount);
        }

        [TestMethod]
        public async Task Begin_BlankProviderKey_NeverAsksTheResolver()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            await harness.TryBeginAsync(OpenIdConnectAuthHarness.CreateRequest(providerKey: " ", redirectUri: "https://app.example/x"), harness.CreateBrowser());

            Assert.AreEqual(0, harness.Resolver.RequestedKeys.Count);
        }

        [TestMethod]
        public async Task Begin_RedirectUriOfAnotherProvider_ThrowsAndSetsNoCookie()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(redirectUri: OpenIdConnectAuthHarness.SigningRedirectUri(OpenIdConnectAuthHarness.OtherKey)),
                browser));

            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Begin_RedirectUriDifferingOnlyInCase_Throws()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(redirectUri: OpenIdConnectAuthHarness.SigningRedirectUri(OpenIdConnectAuthHarness.PrimaryKey).ToUpperInvariant()),
                harness.CreateBrowser()));
        }

        [TestMethod]
        public async Task Begin_ExpectedIdentityAtAnotherIssuer_Throws()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(expectedIdentity: new OpenIdConnectIdentity(harness.Other.Issuer, FakeOpenIdConnectIssuer.Subject)),
                harness.CreateBrowser()));

            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthBeginRequest.ExpectedIdentity));
        }

        [TestMethod]
        [DataRow(OpenIdConnectAuthHarness.PrimaryKey)]
        [DataRow(OpenIdConnectAuthHarness.TenantsKey)]
        public async Task Begin_DefaultExpectedIdentity_Throws(string providerKey)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(providerKey: providerKey, expectedIdentity: default(OpenIdConnectIdentity)),
                harness.CreateBrowser()));
        }

        [TestMethod]
        public async Task Begin_ExpectedIdentityAtOneTenantOfAMultiTenantProvider_Begins()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(
                    providerKey: OpenIdConnectAuthHarness.TenantsKey,
                    expectedIdentity: new OpenIdConnectIdentity(harness.Tenants.IssuerForTenant("tenant-b"), FakeOpenIdConnectIssuer.Subject)),
                harness.CreateBrowser());

            StringAssert.StartsWith(result.AuthorizationUrl, harness.Tenants.AuthorizationEndpoint);
        }

        [TestMethod]
        [DataRow("https://tenants.fake.test/{tenantid}/v2.0")]
        [DataRow("https://tenants.fake.test//v2.0")]
        [DataRow("https://tenants.fake.test/a/b/v2.0")]
        [DataRow("https://other.fake.test/tenant-a/v2.0")]
        public async Task Begin_ExpectedIdentityNotFittingAMultiTenantProvidersTemplate_Throws(string issuer)
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(
                    providerKey: OpenIdConnectAuthHarness.TenantsKey,
                    expectedIdentity: new OpenIdConnectIdentity(issuer, FakeOpenIdConnectIssuer.Subject)),
                harness.CreateBrowser()));
        }

        [TestMethod]
        public async Task Begin_SeveralProblems_ReportsEveryOneInOneException()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            // A null caller binding on purpose: the request's required property is what an untyped caller can still omit.
            string? missing = null;
            OpenIdConnectAuthBeginRequest request = new OpenIdConnectAuthBeginRequest
            {
                ProviderKey = OpenIdConnectAuthHarness.PrimaryKey,
                RedirectUri = "https://elsewhere.example/callback",
                CallerBinding = missing!,
                Prompt = (OpenIdConnectPrompt)99,
                Nonce = " ",
                MaxAuthenticationAge = TimeSpan.FromMilliseconds(500),
                AcceptedAcrValues = [],
                AcceptedAmrValues = ["otp", " "],
                RequiredClaimValues = new Dictionary<string, string> { ["acrs"] = " " },
            };

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => harness.BeginAsync(request, harness.CreateBrowser()));

            StringAssert.Contains(exception.Message, "elsewhere.example");
            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthBeginRequest.CallerBinding));
            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthBeginRequest.Prompt));
            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthBeginRequest.Nonce));
            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthBeginRequest.MaxAuthenticationAge));
            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthBeginRequest.AcceptedAcrValues));
            StringAssert.Contains(exception.Message, nameof(OpenIdConnectAuthBeginRequest.AcceptedAmrValues));
            StringAssert.Contains(exception.Message, "required claim 'acrs'");
        }

        [TestMethod]
        public async Task Begin_DiscoveryDocumentForAnotherIssuer_ThrowsInvalidOperationAndSetsNoCookie()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.DiscoveryIssuer = harness.Other.Issuer;
            FakeBrowser browser = harness.CreateBrowser();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser));

            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Begin_DiscoveryDocumentNamingAnHttpAuthorizeEndpoint_ThrowsInvalidOperation()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.AuthorizationEndpoint = "http://primary.fake.test/authorize";

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), harness.CreateBrowser()));
        }

        [TestMethod]
        public async Task Begin_DiscoveryDocumentUnavailable_ThrowsInvalidOperationAndSetsNoCookie()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            harness.Primary.MetadataStatusOverride = HttpStatusCode.ServiceUnavailable;
            FakeBrowser browser = harness.CreateBrowser();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser));

            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Begin_TwoProviders_EachFetchesItsOwnDiscoveryDocumentOnce()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();

            await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);
            OpenIdConnectAuthBeginResult other = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(providerKey: OpenIdConnectAuthHarness.OtherKey), browser);

            Assert.AreEqual(1, harness.Primary.MetadataRequestCount);
            Assert.AreEqual(1, harness.Other.MetadataRequestCount);
            StringAssert.StartsWith(other.AuthorizationUrl, harness.Other.AuthorizationEndpoint);
        }

        [TestMethod]
        public async Task Begin_ProviderChangedToAnotherIssuer_UsesThatIssuersDiscoveryDocument()
        {
            using OpenIdConnectAuthHarness harness = OpenIdConnectAuthHarness.Create();
            FakeBrowser browser = harness.CreateBrowser();
            await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            harness.ChangeProvider(OpenIdConnectAuthHarness.PrimaryKey, issuer: OpenIdConnectIssuer.Fixed(harness.Other.Issuer));
            OpenIdConnectAuthBeginResult result = await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser);

            StringAssert.StartsWith(result.AuthorizationUrl, harness.Other.AuthorizationEndpoint);
            Assert.AreEqual(1, harness.Other.MetadataRequestCount);
        }

        private static async Task<IReadOnlyList<OpenIdConnectAuthBeginResult>> BeginUpToTheLimitAsync(OpenIdConnectAuthHarness harness, FakeBrowser browser)
        {
            List<OpenIdConnectAuthBeginResult> pending = new List<OpenIdConnectAuthBeginResult>();
            for (int flow = 0; flow < FlowBindingCookie.MaxPendingFlows; flow++)
            {
                pending.Add(await harness.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(), browser));
            }

            return pending;
        }
    }
}
