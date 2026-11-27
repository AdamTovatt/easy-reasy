using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The sign-in endpoints: begin with the identity provider the browser chose and the handler's settings, and complete
    /// with the code the completion page received, audited like every provider package's sign-in.
    /// </summary>
    [TestClass]
    public class SamlAuthEndpointTests
    {
        [TestMethod]
        public async Task SignIn_FullFlow_IssuesTheHandlersAuthResponseAndAuditsTheAccount()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(auditLogger: auditLogger);
            SamlBrowser browser = new SamlBrowser();

            string redirectUrl = await host.BeginAsync(browser);
            string code = await host.AnswerAsync(redirectUrl);
            HttpResponseMessage response = await host.PostSignInAsync(browser, code);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(host.Handler.ResponseToReturn!.Token, json.RootElement.GetProperty("token").GetString());
            SamlUserInfo user = host.Handler.HandledUsers.Single();
            Assert.AreEqual(SamlFlowHost.DefaultIdentity, user.Identity);
            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.IsTrue(audited.Success);
            Assert.AreEqual("saml", audited.Provider);
            Assert.AreEqual(user.Identity.ToString(), audited.AttemptedSubject);
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task Begin_HandlerSettings_DecideTheCompletionPathAndTheRules()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            host.Handler.Settings = new SamlSignInSettings
            {
                CompletionPath = "/auth/saml/done",
                ForceAuthentication = true,
                AcceptedAuthnContextClassRefs = ["urn:example:mfa"],
            };
            SamlBrowser browser = new SamlBrowser();

            string redirectUrl = await host.BeginAsync(browser);
            HttpResponseMessage answered = await host.PostAssertionAsync(host.Respond(redirectUrl, response => response.AuthnContextClassRef = "urn:example:mfa"));

            SamlRedirectReader request = new SamlRedirectReader(redirectUrl);
            Assert.AreEqual("true", request.Root.GetAttribute("ForceAuthn"));
            Assert.IsNotNull(request.Child("RequestedAuthnContext"));
            StringAssert.StartsWith(answered.Headers.Location!.OriginalString, "/auth/saml/done?code=");
            CollectionAssert.AreEqual(new[] { SamlFlowHost.IdentityProviderKey }, host.Handler.SettingsRequestedFor);
        }

        [TestMethod]
        [DataRow("nobody")]
        [DataRow("")]
        [DataRow(null)]
        public async Task Begin_UnknownIdentityProvider_Returns400WithoutAskingTheHandler(string? identityProviderKey)
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            SamlBrowser browser = new SamlBrowser();

            HttpResponseMessage response = await host.PostBeginAsync(browser, identityProviderKey);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(0, host.Handler.SettingsRequestedFor.Count);
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task SignIn_CodeFromAnApplicationsOwnFlow_IsRefusedAsCallerBindingMismatch()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(auditLogger: auditLogger);
            SamlBrowser browser = new SamlBrowser();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest());
            string code = await host.AnswerAsync(begun!.RedirectUrl);

            HttpResponseMessage response = await host.PostSignInAsync(browser, code);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(0, host.Handler.HandledUsers.Count);
            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.AreEqual(ExternalAuthFailureReason.InvalidToken, audited.FailureReason);
            Assert.AreEqual(nameof(SamlAuthFailureReason.CallerBindingMismatch), audited.FailureDetail);
        }

        [TestMethod]
        public async Task SignIn_RefusedByTheRules_IsAuditedAsRejectedNamingTheAccount()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(auditLogger: auditLogger);
            host.Handler.Settings = new SamlSignInSettings { CompletionPath = "/signed-in", AcceptedAuthnContextClassRefs = ["urn:example:mfa"] };
            SamlBrowser browser = new SamlBrowser();

            string code = await host.AnswerAsync(await host.BeginAsync(browser));
            HttpResponseMessage response = await host.PostSignInAsync(browser, code);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            ExternalAuthResult audited = auditLogger.ExternalAuthCalls.Single().Result;
            Assert.AreEqual(ExternalAuthFailureReason.Rejected, audited.FailureReason);
            Assert.AreEqual(nameof(SamlAuthFailureReason.AuthnContextClassNotAccepted), audited.FailureDetail);
            Assert.AreEqual(SamlFlowHost.DefaultIdentity.ToString(), audited.AttemptedSubject);
        }

        [TestMethod]
        public async Task SignIn_HandlerRejectsTheUser_Returns401()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            host.Handler.ResponseToReturn = null;
            SamlBrowser browser = new SamlBrowser();

            string code = await host.AnswerAsync(await host.BeginAsync(browser));
            HttpResponseMessage response = await host.PostSignInAsync(browser, code);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(1, host.Handler.HandledUsers.Count);
        }

        [TestMethod]
        public async Task SignIn_UnknownCode_Returns401()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            HttpResponseMessage response = await host.PostSignInAsync(new SamlBrowser(), "never-issued");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public void GetCategory_EveryFailureReason_HasACategory()
        {
            foreach (SamlAuthFailureReason reason in Enum.GetValues<SamlAuthFailureReason>())
            {
                ExternalAuthFailureReason category = SamlSignInService.GetCategory(reason);
                Assert.IsTrue(category is ExternalAuthFailureReason.InvalidToken or ExternalAuthFailureReason.Rejected, reason.ToString());
            }
        }

        [TestMethod]
        public async Task AddSamlAuthEndpoints_MissingServices_ThrowsNamingEveryOne()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => app.AddSamlAuthEndpoints());

            StringAssert.Contains(exception.Message, nameof(ISamlAuthService));
            StringAssert.Contains(exception.Message, nameof(ISamlIdentityProviderResolver));
            StringAssert.Contains(exception.Message, nameof(ISamlAuthStore));
            StringAssert.Contains(exception.Message, nameof(ISamlAuthHandler));
            StringAssert.Contains(exception.Message, nameof(IJwtTokenService));
        }

        [TestMethod]
        public void AddEasyReasySamlAuth_InvalidOptions_ThrowsListingEveryProblem()
        {
            ServiceCollection services = new ServiceCollection();
            SamlAuthOptions options = new SamlAuthOptions
            {
                ServiceProviderEntityId = " ",
                AssertionConsumerServiceUrl = new Uri("/acs", UriKind.Relative),
                StateKey = new byte[16],
                RequestLifetime = TimeSpan.Zero,
                SweepInterval = TimeSpan.FromDays(50),
            };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => services.AddEasyReasySamlAuth(options));

            StringAssert.Contains(exception.Message, nameof(SamlAuthOptions.ServiceProviderEntityId));
            StringAssert.Contains(exception.Message, nameof(SamlAuthOptions.AssertionConsumerServiceUrl));
            StringAssert.Contains(exception.Message, nameof(SamlAuthOptions.StateKey));
            StringAssert.Contains(exception.Message, nameof(SamlAuthOptions.RequestLifetime));
            StringAssert.Contains(exception.Message, nameof(SamlAuthOptions.SweepInterval));
        }

        [TestMethod]
        public void AddEasyReasySamlAuth_SigningCertificateWithoutPrivateKey_Throws()
        {
            SamlAuthOptions options = SamlValidationHarness.CreateOptions([], SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption));

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new ServiceCollection().AddEasyReasySamlAuth(options));

            StringAssert.Contains(exception.Message, nameof(SamlAuthOptions.SigningCertificate));
        }

        [TestMethod]
        public async Task Sweep_ExpiredAndLiveRows_DeletesOnlyTheExpired()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            await host.ServiceBeginAsync(new SamlBrowser(), SamlFlowHost.AppRequest());
            await host.BeginAndAnswerAsync(new SamlBrowser());
            host.Clock.Advance(host.Options.RequestLifetime);
            await host.ServiceBeginAsync(new SamlBrowser(), SamlFlowHost.AppRequest());
            await host.BeginAndAnswerAsync(new SamlBrowser());

            await HostedSweep(host).SweepOnceAsync(CancellationToken.None);

            Assert.AreEqual(1, host.Store.PendingRequests.Count);
            Assert.AreEqual(1, host.Store.Completions.Count);
            Assert.IsTrue(host.Store.PendingRequests.Single().ExpiresAt > host.Clock.GetUtcNow().UtcDateTime);
            Assert.IsTrue(host.Store.Completions.Single().ExpiresAt > host.Clock.GetUtcNow().UtcDateTime);
        }

        [TestMethod]
        public async Task Sweep_IntervalPasses_SweepsWithoutACall()
        {
            // The rows' time is the test clock; the sweep's timer runs on real time, so the interval is short.
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(sweepInterval: TimeSpan.FromMilliseconds(20));
            await host.ServiceBeginAsync(new SamlBrowser(), SamlFlowHost.AppRequest());
            host.Clock.Advance(host.Options.RequestLifetime);

            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (host.Store.PendingRequests.Count > 0)
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "Timed out waiting for the timer's sweep.");
                await Task.Delay(10);
            }

            Assert.AreEqual(host.Options.SweepInterval, HostedSweep(host).Interval);
        }

        [TestMethod]
        public async Task SignIn_HandlerMaxAuthenticationAge_RefusesAnOlderAuthentication()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(auditLogger: auditLogger);
            host.Handler.Settings = new SamlSignInSettings { CompletionPath = "/signed-in", MaxAuthenticationAge = TimeSpan.FromMinutes(5) };
            SamlBrowser browser = new SamlBrowser();

            string redirectUrl = await host.BeginAsync(browser);
            string code = await host.AnswerAsync(redirectUrl, response => response.AuthenticationInstant = host.Clock.GetUtcNow().AddMinutes(-6));
            HttpResponseMessage response = await host.PostSignInAsync(browser, code);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(nameof(SamlAuthFailureReason.StaleAuthentication), auditLogger.ExternalAuthCalls.Single().Result.FailureDetail);
            Assert.AreEqual(0, host.Handler.HandledUsers.Count);
        }

        [TestMethod]
        public async Task Begin_KnownIdentityProvider_AnswersNoStore()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            HttpResponseMessage response = await host.PostBeginAsync(new SamlBrowser());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
        }

        [TestMethod]
        public async Task AddSamlAuthEndpoints_MetadataOnOneBuilder_ReachesOnlyThatEndpoint()
        {
            RateLimitMarker marker = new RateLimitMarker();
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(configureEndpoints: endpoints => endpoints.AssertionConsumerService.WithMetadata(marker));

            Dictionary<string, bool> marked = host.App.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/auth/saml", StringComparison.Ordinal))
                .ToDictionary(endpoint => endpoint.RoutePattern.RawText!, endpoint => endpoint.Metadata.GetMetadata<RateLimitMarker>() != null);

            Assert.AreEqual(3, marked.Count);
            Assert.IsTrue(marked[SamlAuthPaths.AssertionConsumerService]);
            Assert.IsFalse(marked[SamlAuthPaths.Begin]);
            Assert.IsFalse(marked[SamlAuthPaths.SignIn]);
        }

        private static SamlAuthSweep HostedSweep(SamlFlowHost host)
        {
            return host.App.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<SamlAuthSweep>().Single();
        }

        private sealed class RateLimitMarker
        {
        }
    }
}
