using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Net.Http.Json;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// The two sign-in endpoints on a real <see cref="WebApplication"/> and <c>TestServer</c>, against the fake
    /// identity provider: what begin sends Microsoft, what the handler receives, what each attempt records in the
    /// audit hook, the access-token cookie, and the startup checks. How each refusal is detected is
    /// <c>MicrosoftAuthServiceCompleteTests</c>' concern, and how each reason is categorised is
    /// <c>MicrosoftSignInServiceTests</c>'; these check that the endpoint carries both through.
    /// </summary>
    [TestClass]
    public class MicrosoftAuthEndpointTests
    {
        private const string BindingCookiePrefix = "__Host-easyreasy-microsoft-";

        [TestMethod]
        public async Task Begin_HandlerSettings_AreWhatMicrosoftIsAskedForAndTheBindingCookieIsSet()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();
            host.Handler.Settings = new MicrosoftSignInSettings
            {
                RedirectUri = MicrosoftAuthHarness.SignInRedirectUri,
                TenantId = FakeMicrosoftIdentityProvider.Tenant,
                Prompt = MicrosoftAuthPrompt.Login,
                RequiredAuthenticationContextId = "c1",
                MaxAuthenticationAge = TimeSpan.FromMinutes(5),
            };
            FakeBrowser browser = host.CreateBrowser();

            HttpResponseMessage response = await host.PostBeginAsync(browser, new { loginHint = "ada@contoso.example" });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            string authorizationUrl = (await response.Content.ReadFromJsonAsync<MicrosoftSignInBeginResponse>())!.AuthorizationUrl;
            StringAssert.StartsWith(authorizationUrl, $"{FakeMicrosoftIdentityProvider.Authority}/{FakeMicrosoftIdentityProvider.TenantId}/oauth2/v2.0/authorize?");
            Dictionary<string, StringValues> query = ReadQuery(authorizationUrl);
            Assert.AreEqual(MicrosoftAuthHarness.SignInRedirectUri, query["redirect_uri"].ToString());
            Assert.AreEqual("login", query["prompt"].ToString());
            Assert.AreEqual("ada@contoso.example", query["login_hint"].ToString());
            Assert.AreEqual("300", query["max_age"].ToString());
            StringAssert.Contains(query["claims"].ToString(), "\"c1\"");
            Assert.AreEqual(1, BindingCookieCount(browser));
            CollectionAssert.AreEqual(new[] { "/api/auth/microsoft/begin" }, host.Handler.ReceivedRequestPaths);
        }

        [TestMethod]
        public async Task Begin_NoBody_BeginsWithoutALoginHint()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();

            string authorizationUrl = await host.BeginAsync(host.CreateBrowser());

            Assert.IsFalse(ReadQuery(authorizationUrl).ContainsKey("login_hint"));
            StringAssert.StartsWith(authorizationUrl, $"{FakeMicrosoftIdentityProvider.Authority}/organizations/oauth2/v2.0/authorize?");
        }

        [TestMethod]
        [DataRow("   ")]
        [DataRow("")]
        public async Task Begin_BlankLoginHint_SendsNone(string loginHint)
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();

            string authorizationUrl = await host.BeginAsync(host.CreateBrowser(), new { loginHint });

            Assert.IsFalse(ReadQuery(authorizationUrl).ContainsKey("login_hint"));
        }

        [TestMethod]
        public async Task Begin_LoginHintOverTheLimit_SendsNoneAndOneAtTheLimitIsSent()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();
            string atTheLimit = new string('a', MicrosoftSignInService.MaxLoginHintLength);

            string withLongHint = await host.BeginAsync(host.CreateBrowser(), new { loginHint = atTheLimit + "a" });
            string withHintAtTheLimit = await host.BeginAsync(host.CreateBrowser(), new { loginHint = atTheLimit });

            Assert.IsFalse(ReadQuery(withLongHint).ContainsKey("login_hint"));
            Assert.AreEqual(atTheLimit, ReadQuery(withHintAtTheLimit)["login_hint"].ToString());
        }

        [TestMethod]
        public async Task Begin_BodyNamingATenantRedirectUriOrPrompt_IgnoresAllButTheLoginHint()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();

            string authorizationUrl = await host.BeginAsync(host.CreateBrowser(), new
            {
                loginHint = "ada@contoso.example",
                tenantId = FakeMicrosoftIdentityProvider.OtherTenantId,
                redirectUri = MicrosoftAuthHarness.SigningRedirectUri,
                prompt = "none",
            });

            StringAssert.StartsWith(authorizationUrl, $"{FakeMicrosoftIdentityProvider.Authority}/organizations/oauth2/v2.0/authorize?");
            Dictionary<string, StringValues> query = ReadQuery(authorizationUrl);
            Assert.AreEqual(MicrosoftAuthHarness.SignInRedirectUri, query["redirect_uri"].ToString());
            Assert.IsFalse(query.ContainsKey("prompt"));
        }

        [TestMethod]
        public async Task Begin_AuditLoggerRegistered_RecordsNothing()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);

            await host.BeginAsync(host.CreateBrowser());

            Assert.AreEqual(0, auditLogger.ExternalAuthCalls.Count);
        }

        [TestMethod]
        public async Task Begin_HandlerSettingsNamingARedirectUriNotAllowed_FailsTheRequestWithoutACookie()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();
            host.Handler.Settings = new MicrosoftSignInSettings { RedirectUri = "https://elsewhere.example/callback" };
            FakeBrowser browser = host.CreateBrowser();

            // A configuration error, not a refusal. Under TestServer the exception surfaces on the call; in a hosted
            // server it would be a 500.
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => host.PostBeginAsync(browser));
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task SignIn_ValidFlow_ReturnsTheResponseHandsTheHandlerTheIdentityAndAuditsTheSuccess()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            long authTime = MicrosoftAuthHarness.StartTime.ToUnixTimeSeconds() - 60;
            FakeBrowser browser = host.CreateBrowser();

            HttpResponseMessage response = await host.SignInAsync(browser, token =>
            {
                token.Claims["auth_time"] = authTime;
                token.Claims["acrs"] = new[] { "c1", "c2" };
            });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.AreEqual(host.Handler.ResponseToReturn!.Token, (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token);

            // The six fields issue #46's done-when names for the end-to-end sign-in; the rest of MicrosoftUserInfo is
            // Complete_ValidFlow_ReturnsTheIdentityAndTheCallerBinding's.
            MicrosoftUserInfo user = host.Handler.ReceivedUsers.Single();
            Assert.AreEqual(FakeMicrosoftIdentityProvider.Tenant, user.TenantId);
            Assert.AreEqual(FakeMicrosoftIdentityProvider.ObjectId, user.ObjectId);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(authTime), user.AuthenticationTime);
            Assert.IsFalse(string.IsNullOrEmpty(user.Nonce));
            CollectionAssert.AreEqual(new[] { "pwd", "mfa" }, user.AuthenticationMethods.ToArray());
            CollectionAssert.AreEqual(new[] { "c1", "c2" }, user.AuthenticationContextIds.ToArray());
            CollectionAssert.AreEqual(new[] { "/api/auth/microsoft/begin", "/api/auth/microsoft" }, host.Handler.ReceivedRequestPaths);

            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.IsTrue(audited.Success);
            Assert.AreEqual("microsoft", audited.Provider);
            Assert.AreEqual($"{FakeMicrosoftIdentityProvider.TenantId}:{FakeMicrosoftIdentityProvider.ObjectId}", audited.AttemptedSubject);
            Assert.IsNull(audited.FailureDetail);
            Assert.AreEqual(0, BindingCookieCount(browser));
        }

        [TestMethod]
        public async Task SignIn_CompletedInAnotherBrowser_IsRefusedAndAuditedAsAMissingBindingCookie()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            string authorizationUrl = await host.BeginAsync(host.CreateBrowser());
            string code = host.IdentityProvider.IssueCode(authorizationUrl);

            HttpResponseMessage response = await host.PostSignInAsync(host.CreateBrowser(), code, MicrosoftAuthEndpointHost.ReadState(authorizationUrl));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            AssertAudited(auditLogger, ExternalAuthFailureReason.InvalidToken, MicrosoftAuthFailureReason.BindingCookieMissing, expectSubject: false);
            Assert.AreEqual(0, host.Handler.ReceivedUsers.Count);
        }

        [TestMethod]
        public async Task SignIn_BindingCookieOverwrittenWithAnotherValue_IsRefusedAndAuditedAsAMismatch()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            FakeBrowser browser = host.CreateBrowser();
            string authorizationUrl = await host.BeginAsync(browser);
            string code = host.IdentityProvider.IssueCode(authorizationUrl);
            browser.Set(browser.Cookies.Keys.Single(name => name.StartsWith(BindingCookiePrefix, StringComparison.Ordinal)), "planted-value");

            HttpResponseMessage response = await host.PostSignInAsync(browser, code, MicrosoftAuthEndpointHost.ReadState(authorizationUrl));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertAudited(auditLogger, ExternalAuthFailureReason.InvalidToken, MicrosoftAuthFailureReason.BindingCookieMismatch, expectSubject: false);
        }

        [TestMethod]
        public async Task SignIn_StateOlderThanItsLifetime_IsRefusedAndAuditedAsExpired()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            TimeSpan stateLifetime = TimeSpan.FromMinutes(3);
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(
                auditLogger, options: MicrosoftAuthHarness.CreateOptions(stateLifetime: stateLifetime));
            FakeBrowser browser = host.CreateBrowser();
            string authorizationUrl = await host.BeginAsync(browser);
            string code = host.IdentityProvider.IssueCode(authorizationUrl);
            host.Clock.Advance(stateLifetime);

            HttpResponseMessage response = await host.PostSignInAsync(browser, code, MicrosoftAuthEndpointHost.ReadState(authorizationUrl));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertAudited(auditLogger, ExternalAuthFailureReason.InvalidToken, MicrosoftAuthFailureReason.ExpiredState, expectSubject: false);
        }

        [TestMethod]
        public async Task SignIn_StateThatIsNotThisServers_IsRefusedAndAuditedAsInvalidState()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);

            HttpResponseMessage response = await host.PostSignInAsync(host.CreateBrowser(), "code", "not-a-state");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertAudited(auditLogger, ExternalAuthFailureReason.InvalidToken, MicrosoftAuthFailureReason.InvalidState, expectSubject: false);
        }

        [TestMethod]
        public async Task SignIn_SameCodeAndStatePostedTwice_SignsInOnceAndRefusesTheReplay()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            FakeBrowser browser = host.CreateBrowser();
            string authorizationUrl = await host.BeginAsync(browser);
            string code = host.IdentityProvider.IssueCode(authorizationUrl);
            string state = MicrosoftAuthEndpointHost.ReadState(authorizationUrl);

            HttpResponseMessage first = await host.PostSignInAsync(browser, code, state);
            HttpResponseMessage replay = await host.PostSignInAsync(browser, code, state);

            Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, replay.StatusCode);
            Assert.AreEqual(2, auditLogger.ExternalAuthCalls.Count);
            Assert.IsTrue(auditLogger.ExternalAuthCalls[0].Result.Success);
            Assert.AreEqual(nameof(MicrosoftAuthFailureReason.BindingCookieMissing), auditLogger.ExternalAuthCalls[1].Result.FailureDetail);
            Assert.AreEqual(1, host.Handler.ReceivedUsers.Count);
        }

        [TestMethod]
        public async Task SignIn_TokenFromAnotherTenantThanTheSettingsName_IsRejectedAndAuditedWithTheAccount()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            host.Handler.Settings = new MicrosoftSignInSettings
            {
                RedirectUri = MicrosoftAuthHarness.SignInRedirectUri,
                TenantId = FakeMicrosoftIdentityProvider.Tenant,
            };

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser(), token =>
            {
                token.Claims["tid"] = FakeMicrosoftIdentityProvider.OtherTenantId;
                token.Claims["iss"] = $"{FakeMicrosoftIdentityProvider.Authority}/{FakeMicrosoftIdentityProvider.OtherTenantId}/v2.0";
            });

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            ExternalAuthResult audited = AssertAudited(auditLogger, ExternalAuthFailureReason.Rejected, MicrosoftAuthFailureReason.TenantMismatch, expectSubject: true);
            Assert.AreEqual($"{FakeMicrosoftIdentityProvider.OtherTenantId}:{FakeMicrosoftIdentityProvider.ObjectId}", audited.AttemptedSubject);
            Assert.AreEqual(0, host.Handler.ReceivedUsers.Count);
        }

        [TestMethod]
        public async Task SignIn_TokenWithoutTheRequiredAuthenticationContext_IsRejectedAndAudited()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            host.Handler.Settings = new MicrosoftSignInSettings
            {
                RedirectUri = MicrosoftAuthHarness.SignInRedirectUri,
                RequiredAuthenticationContextId = "c3",
            };

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertAudited(auditLogger, ExternalAuthFailureReason.Rejected, MicrosoftAuthFailureReason.MissingAuthenticationContext, expectSubject: true);
        }

        [TestMethod]
        public async Task SignIn_StateTheApplicationBeganForItsOwnFlow_IsRefusedAsACallerBindingMismatch()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            FakeBrowser browser = host.CreateBrowser();
            DefaultHttpContext beginContext = new DefaultHttpContext();
            MicrosoftAuthBeginResult begun = host.App.Services.GetRequiredService<IMicrosoftAuthService>().Begin(
                MicrosoftAuthHarness.CreateRequest(redirectUri: MicrosoftAuthHarness.SigningRedirectUri), beginContext);
            browser.Absorb(beginContext.Response);
            string code = host.IdentityProvider.IssueCode(begun.AuthorizationUrl);

            HttpResponseMessage response = await host.PostSignInAsync(browser, code, begun.State);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertAudited(auditLogger, ExternalAuthFailureReason.InvalidToken, MicrosoftAuthFailureReason.CallerBindingMismatch, expectSubject: true);
            Assert.AreEqual(0, host.Handler.ReceivedUsers.Count);
        }

        [TestMethod]
        public async Task SignIn_HandlerReturnsNull_IsRejectedWithTheAccountAndNoDetail()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            host.Handler.ResponseToReturn = null;

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.AreEqual(ExternalAuthFailureReason.Rejected, audited.FailureReason);
            Assert.IsNotNull(audited.AttemptedSubject);
            Assert.IsNull(audited.FailureDetail);
        }

        [TestMethod]
        public async Task SignIn_TwoFlowsBegunInOneBrowser_EachCompletesInEitherOrder()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();
            FakeBrowser browser = host.CreateBrowser();
            string firstUrl = await host.BeginAsync(browser);
            string secondUrl = await host.BeginAsync(browser);
            string firstCode = host.IdentityProvider.IssueCode(firstUrl);
            string secondCode = host.IdentityProvider.IssueCode(secondUrl);

            HttpResponseMessage second = await host.PostSignInAsync(browser, secondCode, MicrosoftAuthEndpointHost.ReadState(secondUrl));
            HttpResponseMessage first = await host.PostSignInAsync(browser, firstCode, MicrosoftAuthEndpointHost.ReadState(firstUrl));

            Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
        }

        [TestMethod]
        public async Task SignIn_NoAuditLogger_ReturnsOk()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task SignIn_AuditLoggerThrows_PropagatesOutOfTheEndpoint()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(new ThrowingExternalAuthLogger());

            // Under TestServer the unhandled exception surfaces on the call; in a hosted server it would be a 500.
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => host.SignInAsync(host.CreateBrowser()));
        }

        [TestMethod]
        public async Task SignIn_TokenEndpointFails_PropagatesAndIsNotAudited()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(auditLogger);
            host.IdentityProvider.TokenEndpointStatusOverride = HttpStatusCode.ServiceUnavailable;

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => host.SignInAsync(host.CreateBrowser()));
            Assert.AreEqual(0, auditLogger.ExternalAuthCalls.Count);
        }

        [TestMethod]
        public async Task SignIn_RefreshTokenServiceRegistered_PassesItToTheHandler()
        {
            InertRefreshTokenService refreshTokenService = new InertRefreshTokenService();
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(refreshTokenService: refreshTokenService);

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreSame(refreshTokenService, host.Handler.LastReceivedRefreshTokenService);
        }

        [TestMethod]
        public async Task SignIn_CookieOptionOnAndSignInSucceeds_SetsTheAccessTokenCookie()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(cookieTokenPaths: ["/files"]);

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            string cookie = AccessTokenCookies(response).Single();
            StringAssert.StartsWith(cookie, $"__Secure-access_token={host.Handler.ResponseToReturn!.Token}; max-age=300;");
            StringAssert.Contains(cookie, "; path=/files; secure; samesite=strict; httponly");
        }

        [TestMethod]
        public async Task SignIn_CookieOptionOnAndSignInRefused_SetsNoAccessTokenCookie()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync(cookieTokenPaths: ["/files"]);
            host.Handler.ResponseToReturn = null;

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(0, AccessTokenCookies(response).Length);
        }

        [TestMethod]
        public async Task SignIn_CookieOptionOffAndSignInSucceeds_SetsNoAccessTokenCookie()
        {
            await using MicrosoftAuthEndpointHost host = await MicrosoftAuthEndpointHost.StartAsync();

            HttpResponseMessage response = await host.SignInAsync(host.CreateBrowser());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(0, AccessTokenCookies(response).Length);
        }

        [TestMethod]
        [DataRow("Production")]
        [DataRow("Development")]
        public async Task AddMicrosoftAuthEndpoints_WithoutHandler_ThrowsTheMissingHandlerMessageAtStartup(string environment)
        {
            await AssertStartupFails(environment, services =>
            {
                services.AddEasyReasyAuth(MicrosoftAuthEndpointHost.JwtSecret);
                services.AddEasyReasyMicrosoftAuth(MicrosoftAuthHarness.CreateOptions());
            }, nameof(IMicrosoftAuthHandler));
        }

        [TestMethod]
        public async Task AddMicrosoftAuthEndpoints_WithoutMicrosoftAuth_ThrowsAtStartup()
        {
            await AssertStartupFails(Environments.Production, services =>
            {
                services.AddEasyReasyAuth(MicrosoftAuthEndpointHost.JwtSecret);
                services.AddScoped<IMicrosoftAuthHandler, FakeMicrosoftAuthHandler>();
            }, nameof(IMicrosoftAuthService));
        }

        [TestMethod]
        public async Task AddMicrosoftAuthEndpoints_WithoutEasyReasyAuth_ThrowsAtStartup()
        {
            await AssertStartupFails(Environments.Production, services =>
            {
                services.AddEasyReasyMicrosoftAuth(MicrosoftAuthHarness.CreateOptions());
                services.AddScoped<IMicrosoftAuthHandler, FakeMicrosoftAuthHandler>();
            }, nameof(IJwtTokenService));
        }

        [TestMethod]
        public async Task AddMicrosoftAuthEndpoints_NothingRegistered_ReportsEveryMissingServiceInOneException()
        {
            InvalidOperationException exception = await AssertStartupFails(Environments.Production, services => { }, nameof(IMicrosoftAuthService));

            StringAssert.Contains(exception.Message, nameof(IMicrosoftAuthHandler));
            StringAssert.Contains(exception.Message, nameof(IJwtTokenService));
        }

        [TestMethod]
        public async Task Build_DevelopmentAppUsingOnlyTheServiceWithoutAHandler_BuildsAndResolvesIt()
        {
            // Development validates every registration at Build(), so a registration that needs a handler would fail
            // an application that only calls IMicrosoftAuthService from its own endpoints.
            WebApplicationBuilder builder = CreateBuilder(Environments.Development);
            builder.Services.AddEasyReasyAuth(MicrosoftAuthEndpointHost.JwtSecret);
            builder.Services.AddEasyReasyMicrosoftAuth(MicrosoftAuthHarness.CreateOptions());

            await using WebApplication app = builder.Build();

            Assert.IsNotNull(app.Services.GetRequiredService<IMicrosoftAuthService>());
        }

        private static ExternalAuthResult AssertAudited(
            RecordingAuditLogger auditLogger,
            ExternalAuthFailureReason category,
            MicrosoftAuthFailureReason reason,
            bool expectSubject)
        {
            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.IsFalse(audited.Success);
            Assert.AreEqual("microsoft", audited.Provider);
            Assert.AreEqual(category, audited.FailureReason);
            Assert.AreEqual(reason.ToString(), audited.FailureDetail);
            Assert.AreEqual(expectSubject, audited.AttemptedSubject != null, audited.AttemptedSubject);
            return audited;
        }

        /// <summary>
        /// Builds an application in the given environment with only what <paramref name="register"/> adds, and
        /// asserts that mapping the endpoints fails with a message naming <paramref name="expectedInMessage"/>.
        /// </summary>
        private static async Task<InvalidOperationException> AssertStartupFails(string environment, Action<IServiceCollection> register, string expectedInMessage)
        {
            WebApplicationBuilder builder = CreateBuilder(environment);
            register(builder.Services);
            await using WebApplication app = builder.Build();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => app.AddMicrosoftAuthEndpoints());
            StringAssert.Contains(exception.Message, expectedInMessage);
            return exception;
        }

        private static WebApplicationBuilder CreateBuilder(string environment)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.Logging.ClearProviders();
            return builder;
        }

        private static Dictionary<string, StringValues> ReadQuery(string authorizationUrl)
        {
            return QueryHelpers.ParseQuery(new Uri(authorizationUrl).Query);
        }

        private static int BindingCookieCount(FakeBrowser browser)
        {
            return browser.Cookies.Keys.Count(name => name.StartsWith(BindingCookiePrefix, StringComparison.Ordinal));
        }

        private static string[] AccessTokenCookies(HttpResponseMessage response)
        {
            return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
                ? values.Where(value => value.StartsWith("__Secure-access_token=", StringComparison.Ordinal)).ToArray()
                : [];
        }
    }
}
