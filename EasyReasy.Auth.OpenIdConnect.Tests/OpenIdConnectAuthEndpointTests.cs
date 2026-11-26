using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Net.Http.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The two sign-in endpoints on a real <see cref="WebApplication"/> and <c>TestServer</c>, against the fake issuer:
    /// what begin sends the provider, what the handler receives, what each attempt records in the audit hook, the
    /// access-token cookie, and the startup checks. How each refusal is detected is
    /// <c>OpenIdConnectAuthServiceCompleteTests</c>' concern; these check that the endpoints carry it through.
    /// </summary>
    [TestClass]
    public class OpenIdConnectAuthEndpointTests
    {

        [TestMethod]
        public async Task Begin_HandlerSettingsForTheChosenProvider_AreWhatTheProviderIsAskedForAndTheBindingCookieIsSet()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync();
            host.Handler.Settings[OpenIdConnectAuthHarness.PrimaryKey] = new OpenIdConnectSignInSettings
            {
                RedirectUri = OpenIdConnectAuthHarness.SigningRedirectUri(OpenIdConnectAuthHarness.PrimaryKey),
                Prompt = OpenIdConnectPrompt.Login,
                MaxAuthenticationAge = TimeSpan.FromMinutes(5),
                AcceptedAcrValues = ["urn:mfa"],
                AcceptedAmrValues = ["otp"],
                RequiredClaimValues = new Dictionary<string, string> { ["acrs"] = "c1" },
            };
            FakeBrowser browser = host.CreateBrowser();

            HttpResponseMessage response = await host.PostBeginAsync(browser, new { providerKey = OpenIdConnectAuthHarness.PrimaryKey, loginHint = "ada@example.test" });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            string authorizationUrl = (await response.Content.ReadFromJsonAsync<OpenIdConnectSignInBeginResponse>())!.AuthorizationUrl;
            Dictionary<string, StringValues> query = OpenIdConnectAuthHarness.ParseQuery(authorizationUrl);
            StringAssert.StartsWith(authorizationUrl, host.Primary.AuthorizationEndpoint);
            Assert.AreEqual(OpenIdConnectAuthHarness.SigningRedirectUri(OpenIdConnectAuthHarness.PrimaryKey), query["redirect_uri"].ToString());
            Assert.AreEqual("login", query["prompt"].ToString());
            Assert.AreEqual("300", query["max_age"].ToString());
            Assert.AreEqual("ada@example.test", query["login_hint"].ToString());
            StringAssert.Contains(query["claims"].ToString(), "urn:mfa");
            StringAssert.Contains(query["claims"].ToString(), "\"acrs\":{\"essential\":true,\"value\":\"c1\"}");
            CollectionAssert.AreEqual(new[] { OpenIdConnectAuthHarness.PrimaryKey }, host.Handler.SettingsRequestedFor);
            Assert.AreEqual(1, OpenIdConnectAuthHarness.CountBindingCookies(browser));
        }

        [TestMethod]
        public async Task Begin_BodyNamingARedirectUriPromptOrMaxAge_IgnoresAllButTheProviderAndLoginHint()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync();

            HttpResponseMessage response = await host.PostBeginAsync(host.CreateBrowser(), new
            {
                providerKey = OpenIdConnectAuthHarness.PrimaryKey,
                redirectUri = "https://evil.example/callback",
                prompt = "none",
                maxAuthenticationAge = 1,
                acceptedAcrValues = new[] { "weak" },
            });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Dictionary<string, StringValues> query = OpenIdConnectAuthHarness.ParseQuery((await response.Content.ReadFromJsonAsync<OpenIdConnectSignInBeginResponse>())!.AuthorizationUrl);
            Assert.AreEqual(OpenIdConnectAuthHarness.SignInRedirectUri(OpenIdConnectAuthHarness.PrimaryKey), query["redirect_uri"].ToString());
            Assert.IsFalse(query.ContainsKey("prompt"));
            Assert.IsFalse(query.ContainsKey("max_age"));
            Assert.IsFalse(query.ContainsKey("claims"));
        }

        [TestMethod]
        [DataRow("nobody")]
        [DataRow("")]
        [DataRow(" ")]
        public async Task Begin_ProviderKeyResolvingToNoProvider_Answers400WithoutACookie(string providerKey)
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync();
            FakeBrowser browser = host.CreateBrowser();

            HttpResponseMessage response = await host.PostBeginAsync(browser, new { providerKey });

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Begin_ProviderKeyTheHandlerDoesNotOffer_Answers400WithoutResolvingIt()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync();
            host.Handler.NotOffered.Add(OpenIdConnectAuthHarness.PrimaryKey);
            FakeBrowser browser = host.CreateBrowser();

            HttpResponseMessage response = await host.PostBeginAsync(browser, new { providerKey = OpenIdConnectAuthHarness.PrimaryKey });

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(0, browser.Cookies.Count);
            Assert.AreEqual(0, host.Resolver.RequestedKeys.Count);
        }

        [TestMethod]
        public async Task Begin_ProviderKeyTheHandlerOffersButNoProviderHas_Answers400AfterAskingTheHandler()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync();

            HttpResponseMessage response = await host.PostBeginAsync(host.CreateBrowser(), new { providerKey = "nobody" });

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            CollectionAssert.AreEqual(new[] { "nobody" }, host.Handler.SettingsRequestedFor);
        }

        [TestMethod]
        public async Task Begin_NoProviderKey_Answers400()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync();

            HttpResponseMessage response = await host.PostBeginAsync(host.CreateBrowser(), new { loginHint = "ada@example.test" });

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Begin_LoginHintOverTheLimitIsNotSentAndOneAtTheLimitIs()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync();
            string atLimit = new string('a', OpenIdConnectSignInService.MaxLoginHintLength);

            string withLongHint = await host.BeginAsync(host.CreateBrowser(), atLimit + "a");
            string withHintAtLimit = await host.BeginAsync(host.CreateBrowser(), atLimit);

            Assert.IsFalse(OpenIdConnectAuthHarness.ParseQuery(withLongHint).ContainsKey("login_hint"));
            Assert.AreEqual(atLimit, OpenIdConnectAuthHarness.ParseQuery(withHintAtLimit)["login_hint"].ToString());
        }

        [TestMethod]
        public async Task Begin_HandlerSettingsNamingARedirectUriNotTheProviders_FailsTheRequestWithoutACookie()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync();
            host.Handler.Settings[OpenIdConnectAuthHarness.PrimaryKey] = new OpenIdConnectSignInSettings { RedirectUri = "https://elsewhere.example/callback" };
            FakeBrowser browser = host.CreateBrowser();

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => host.PostBeginAsync(browser, new { providerKey = OpenIdConnectAuthHarness.PrimaryKey }));

            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task SignIn_ValidFlow_ReturnsTheResponseHandsTheHandlerTheIdentityAndAuditsTheSuccess()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            FakeBrowser browser = host.CreateBrowser();

            HttpResponseMessage response = await host.SignInAsync(browser);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.AreEqual(host.Handler.ResponseToReturn!.Token, (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token);
            OpenIdConnectIdentity expected = new OpenIdConnectIdentity(host.Primary.Issuer, FakeOpenIdConnectIssuer.Subject);
            OpenIdConnectUserInfo user = host.Handler.ReceivedUsers.Single();
            Assert.AreEqual(expected, user.Identity);
            Assert.AreEqual(OpenIdConnectAuthHarness.PrimaryKey, user.ProviderKey);

            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.IsTrue(audited.Success);
            Assert.AreEqual("oidc", audited.Provider);
            Assert.AreEqual(expected.ToString(), audited.AttemptedSubject);
            Assert.IsNull(audited.FailureDetail);
            Assert.AreEqual(0, OpenIdConnectAuthHarness.CountBindingCookies(browser));
        }

        [TestMethod]
        public async Task SignIn_CompletedInAnotherBrowser_IsRefusedAndAuditedAsAMissingBindingCookie()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            string authorizationUrl = await host.BeginAsync(host.CreateBrowser());

            HttpResponseMessage response = await host.PostSignInAsync(host.CreateBrowser(), host.Primary.IssueCode(authorizationUrl), OpenIdConnectAuthEndpointHost.ReadState(authorizationUrl));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertAudited(auditLogger, ExternalAuthFailureReason.InvalidToken, OpenIdConnectAuthFailureReason.BindingCookieMissing, expectSubject: false);
        }

        [TestMethod]
        public async Task SignIn_PostedFromAnotherRedirectUri_IsRefusedAndAuditedAsARedirectUriMismatch()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            FakeBrowser browser = host.CreateBrowser();
            string authorizationUrl = await host.BeginAsync(browser);

            HttpResponseMessage response = await host.PostSignInAsync(
                browser,
                host.Primary.IssueCode(authorizationUrl),
                OpenIdConnectAuthEndpointHost.ReadState(authorizationUrl),
                redirectUri: OpenIdConnectAuthHarness.SignInRedirectUri(OpenIdConnectAuthHarness.OtherKey));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertAudited(auditLogger, ExternalAuthFailureReason.InvalidToken, OpenIdConnectAuthFailureReason.RedirectUriMismatch, expectSubject: false);
            Assert.AreEqual(0, host.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task SignIn_SameResponsePostedTwice_SignsInOnceAndRefusesTheReplay()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            FakeBrowser browser = host.CreateBrowser();
            string authorizationUrl = await host.BeginAsync(browser);
            string code = host.Primary.IssueCode(authorizationUrl);
            string state = OpenIdConnectAuthEndpointHost.ReadState(authorizationUrl);

            HttpResponseMessage first = await host.PostSignInAsync(browser, code, state);
            HttpResponseMessage replay = await host.PostSignInAsync(browser, code, state);

            Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, replay.StatusCode);
            Assert.AreEqual(OpenIdConnectAuthFailureReason.BindingCookieMissing.ToString(), auditLogger.ExternalAuthCalls[1].Result.FailureDetail);
        }

        [TestMethod]
        public async Task SignIn_TokenRefusedByTheSettingsRules_IsRejectedAndAuditedWithTheAccount()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            host.Handler.Settings[OpenIdConnectAuthHarness.PrimaryKey] = new OpenIdConnectSignInSettings
            {
                RedirectUri = OpenIdConnectAuthHarness.SignInRedirectUri(OpenIdConnectAuthHarness.PrimaryKey),
                AcceptedAcrValues = ["urn:hwk"],
            };

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            ExternalAuthResult audited = AssertAudited(auditLogger, ExternalAuthFailureReason.Rejected, OpenIdConnectAuthFailureReason.AcrNotAccepted, expectSubject: true);
            Assert.AreEqual(new OpenIdConnectIdentity(host.Primary.Issuer, FakeOpenIdConnectIssuer.Subject).ToString(), audited.AttemptedSubject);
            Assert.AreEqual(0, host.Handler.ReceivedUsers.Count);
        }

        [TestMethod]
        public async Task SignIn_StateTheApplicationBeganForItsOwnFlow_IsRefusedAsACallerBindingMismatch()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            FakeBrowser browser = host.CreateBrowser();
            using IServiceScope scope = host.App.Services.CreateScope();
            DefaultHttpContext context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            OpenIdConnectAuthBeginResult? begun = await scope.ServiceProvider.GetRequiredService<IOpenIdConnectAuthService>().BeginAsync(
                OpenIdConnectAuthHarness.CreateRequest(redirectUri: OpenIdConnectAuthHarness.SignInRedirectUri(OpenIdConnectAuthHarness.PrimaryKey)),
                context);
            Assert.IsNotNull(begun);
            browser.Absorb(context.Response);

            HttpResponseMessage response = await host.PostSignInAsync(browser, host.Primary.IssueCode(begun.AuthorizationUrl), begun.State);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            // Refused before the code is redeemed, so no account is known.
            AssertAudited(auditLogger, ExternalAuthFailureReason.InvalidToken, OpenIdConnectAuthFailureReason.CallerBindingMismatch, expectSubject: false);
            Assert.AreEqual(0, host.Handler.ReceivedUsers.Count);
            Assert.AreEqual(0, host.Primary.TokenRequests.Count);
        }

        [TestMethod]
        public async Task SignIn_HandlerReturnsNull_IsRejectedWithTheAccountAndNoDetail()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            host.Handler.ResponseToReturn = null;

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.AreEqual(ExternalAuthFailureReason.Rejected, audited.FailureReason);
            Assert.IsNull(audited.FailureDetail);
            Assert.AreEqual(new OpenIdConnectIdentity(host.Primary.Issuer, FakeOpenIdConnectIssuer.Subject).ToString(), audited.AttemptedSubject);
        }

        [TestMethod]
        public async Task SignIn_TokenEndpointFails_PropagatesAndIsNotAudited()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            host.Primary.TokenEndpointStatusOverride = HttpStatusCode.InternalServerError;

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => host.SignInAsync(host.CreateBrowser()));

            Assert.AreEqual(0, auditLogger.ExternalAuthCalls.Count);
        }

        [TestMethod]
        public async Task SignIn_TokenEndpointRefusesTheClient_PropagatesAndIsNotAudited()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(auditLogger);
            host.Primary.TokenEndpointStatusOverride = HttpStatusCode.Unauthorized;
            host.Primary.TokenEndpointErrorOverride = "invalid_client";

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => host.SignInAsync(host.CreateBrowser()));

            Assert.AreEqual(0, auditLogger.ExternalAuthCalls.Count);
        }

        [TestMethod]
        public async Task SignIn_AuditLoggerThrows_PropagatesOutOfTheEndpoint()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(new ThrowingExternalAuthLogger());

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => host.SignInAsync(host.CreateBrowser()));

            Assert.AreEqual("audit logger failure", exception.Message);
        }

        [TestMethod]
        public async Task SignIn_RefreshTokenServiceRegistered_PassesItToTheHandler()
        {
            InertRefreshTokenService refreshTokenService = new InertRefreshTokenService();
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(refreshTokenService: refreshTokenService);

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreSame(refreshTokenService, host.Handler.LastReceivedRefreshTokenService);
        }

        [TestMethod]
        public async Task SignIn_CookieOptionOnAndSignInSucceeds_SetsTheAccessTokenCookie()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(cookieTokenPaths: ["/files"]);

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            string cookie = AccessTokenCookies(response).Single();
            StringAssert.StartsWith(cookie, $"__Secure-access_token={host.Handler.ResponseToReturn!.Token}; max-age=300;");
        }

        [TestMethod]
        public async Task SignIn_CookieOptionOnAndSignInRefused_SetsNoAccessTokenCookie()
        {
            await using OpenIdConnectAuthEndpointHost host = await OpenIdConnectAuthEndpointHost.StartAsync(cookieTokenPaths: ["/files"]);
            host.Handler.ResponseToReturn = null;

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(0, AccessTokenCookies(response).Length);
        }

        [TestMethod]
        [DataRow("Production")]
        [DataRow("Development")]
        public async Task AddOpenIdConnectAuthEndpoints_WithoutResolverOrHandler_ThrowsNamingBothAtStartup(string environment)
        {
            InvalidOperationException exception = await AssertStartupFails(environment, services =>
            {
                services.AddEasyReasyAuth(OpenIdConnectAuthEndpointHost.JwtSecret);
                services.AddEasyReasyOpenIdConnectAuth(OpenIdConnectAuthHarness.CreateOptions());
            }, nameof(IOpenIdConnectProviderResolver));

            StringAssert.Contains(exception.Message, nameof(IOpenIdConnectAuthHandler));
        }

        [TestMethod]
        public async Task AddOpenIdConnectAuthEndpoints_NothingRegistered_ReportsEveryMissingServiceInOneException()
        {
            InvalidOperationException exception = await AssertStartupFails(Environments.Production, _ => { }, nameof(IOpenIdConnectAuthService));

            StringAssert.Contains(exception.Message, nameof(IOpenIdConnectProviderResolver));
            StringAssert.Contains(exception.Message, nameof(IOpenIdConnectAuthHandler));
            StringAssert.Contains(exception.Message, nameof(IJwtTokenService));
        }

        [TestMethod]
        public async Task Build_DevelopmentAppUsingOnlyTheServiceWithoutAHandler_BuildsAndResolvesIt()
        {
            // Development validates every registration at Build(), so a registration that needs a handler or a resolver
            // would fail an application that registers them later or only calls the service from its own endpoints.
            WebApplicationBuilder builder = CreateBuilder(Environments.Development);
            builder.Services.AddEasyReasyAuth(OpenIdConnectAuthEndpointHost.JwtSecret);
            builder.Services.AddEasyReasyOpenIdConnectAuth(OpenIdConnectAuthHarness.CreateOptions());

            builder.Services.AddScoped<IOpenIdConnectProviderResolver, FakeProviderResolver>();

            await using WebApplication app = builder.Build();

            using IServiceScope scope = app.Services.CreateScope();
            Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<IOpenIdConnectAuthService>());
        }

        private static ExternalAuthResult AssertAudited(
            RecordingAuditLogger auditLogger,
            ExternalAuthFailureReason category,
            OpenIdConnectAuthFailureReason reason,
            bool expectSubject)
        {
            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.IsFalse(audited.Success);
            Assert.AreEqual("oidc", audited.Provider);
            Assert.AreEqual(category, audited.FailureReason);
            Assert.AreEqual(reason.ToString(), audited.FailureDetail);
            Assert.AreEqual(expectSubject, audited.AttemptedSubject != null, audited.AttemptedSubject);
            return audited;
        }

        private static async Task<InvalidOperationException> AssertStartupFails(string environment, Action<IServiceCollection> register, string expectedInMessage)
        {
            WebApplicationBuilder builder = CreateBuilder(environment);
            register(builder.Services);
            await using WebApplication app = builder.Build();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => app.AddOpenIdConnectAuthEndpoints());
            StringAssert.Contains(exception.Message, expectedInMessage);
            return exception;
        }

        private static WebApplicationBuilder CreateBuilder(string environment)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.Logging.ClearProviders();
            return builder;
        }

        private static string[] AccessTokenCookies(HttpResponseMessage response)
        {
            return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
                ? values.Where(value => value.StartsWith("__Secure-access_token=", StringComparison.Ordinal)).ToArray()
                : [];
        }
    }
}
