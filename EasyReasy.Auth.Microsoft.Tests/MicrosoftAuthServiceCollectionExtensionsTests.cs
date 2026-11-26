using EasyReasy.Auth.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth.Microsoft.Tests
{
    [TestClass]
    public class MicrosoftAuthServiceCollectionExtensionsTests
    {
        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_ValidOptions_RegistersTheServiceAndTheSystemClock()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddEasyReasyMicrosoftAuth(MicrosoftAuthHarness.CreateOptions());

            using ServiceProvider provider = services.BuildServiceProvider();
            Assert.IsNotNull(provider.GetRequiredService<IMicrosoftAuthService>());
            Assert.AreSame(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        }

        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_ClockAlreadyRegistered_KeepsIt()
        {
            ServiceCollection services = new ServiceCollection();
            FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
            services.AddSingleton<TimeProvider>(clock);

            services.AddEasyReasyMicrosoftAuth(MicrosoftAuthHarness.CreateOptions());

            using ServiceProvider provider = services.BuildServiceProvider();
            Assert.AreSame(clock, provider.GetRequiredService<TimeProvider>());
        }

        [TestMethod]
        public async Task AddEasyReasyMicrosoftAuth_DefaultAuthorityAndLifetime_SendBeginToMicrosoftsCloudWithATenMinuteCookie()
        {
            MicrosoftAuthOptions options = new MicrosoftAuthOptions
            {
                ClientId = "client",
                ClientSecret = "secret",
                StateKey = MicrosoftAuthHarness.StateKey,
                RedirectUris = [MicrosoftAuthHarness.SignInRedirectUri],
            };
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyMicrosoftAuth(options);
            MicrosoftAuthHarness.ConfigureFakeIdentityProvider(services, new FakeMicrosoftIdentityProvider(new FakeTimeProvider(MicrosoftAuthHarness.StartTime), MicrosoftAuthOptions.DefaultAuthorityBaseUrl));
            using ServiceProvider provider = services.BuildServiceProvider();
            DefaultHttpContext context = new DefaultHttpContext();

            OpenIdConnectAuthBeginResult result = await provider.GetRequiredService<IMicrosoftAuthService>().BeginAsync(
                MicrosoftAuthHarness.CreateRequest(redirectUri: MicrosoftAuthHarness.SignInRedirectUri), context);

            StringAssert.StartsWith(result.AuthorizationUrl, "https://login.microsoftonline.com/organizations/oauth2/v2.0/authorize?");
            SetCookieHeaderValue cookie = SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie.Select(value => value ?? "").ToList()).Single();
            Assert.AreEqual(TimeSpan.FromMinutes(10), cookie.MaxAge);
        }

        [TestMethod]
        public async Task AddEasyReasyMicrosoftAuth_StateKeyChangedAfterRegistration_StillOpensStatesSealedBefore()
        {
            byte[] key = Enumerable.Range(100, 32).Select(value => (byte)value).ToArray();
            using MicrosoftAuthHarness harness = MicrosoftAuthHarness.Create(MicrosoftAuthHarness.CreateOptions(stateKey: key));
            FakeBrowser browser = harness.CreateBrowser();
            OpenIdConnectAuthBeginResult begun = await harness.BeginAsync(MicrosoftAuthHarness.CreateRequest(), browser);

            Array.Clear(key);

            MicrosoftAuthCompletionResult result = await harness.CompleteAsync(harness.IdentityProvider.IssueCode(begun.AuthorizationUrl), begun.State, browser);
            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
        }

        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_BlankClientId_Throws()
        {
            AssertRejected(MicrosoftAuthHarness.CreateOptions(clientId: " "), "client id");
        }

        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_BlankClientSecret_Throws()
        {
            AssertRejected(MicrosoftAuthHarness.CreateOptions(clientSecret: ""), "client secret");
        }

        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_StateKeyNot32Bytes_Throws()
        {
            AssertRejected(MicrosoftAuthHarness.CreateOptions(stateKey: new byte[16]), nameof(MicrosoftAuthOptions.StateKey));
        }

        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_NoRedirectUri_Throws()
        {
            AssertRejected(MicrosoftAuthHarness.CreateOptions(redirectUris: []), "redirect URI");
        }

        [TestMethod]
        [DataRow("/callback")]
        [DataRow("http://app.example/callback")]
        [DataRow("https://app.example/callback#x")]
        public void AddEasyReasyMicrosoftAuth_RedirectUriThatIsRelativeHttpOrHasAFragment_Throws(string redirectUri)
        {
            AssertRejected(MicrosoftAuthHarness.CreateOptions(redirectUris: [redirectUri]), redirectUri);
        }

        [TestMethod]
        public async Task AddEasyReasyMicrosoftAuth_LoopbackHttpRedirectUri_IsAcceptedAndBegunWith()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyMicrosoftAuth(MicrosoftAuthHarness.CreateOptions(redirectUris: ["http://localhost:5000/callback"]));
            MicrosoftAuthHarness.ConfigureFakeIdentityProvider(services, new FakeMicrosoftIdentityProvider(new FakeTimeProvider(MicrosoftAuthHarness.StartTime)));
            using ServiceProvider provider = services.BuildServiceProvider();

            OpenIdConnectAuthBeginResult result = await provider.GetRequiredService<IMicrosoftAuthService>().BeginAsync(
                MicrosoftAuthHarness.CreateRequest(redirectUri: "http://localhost:5000/callback"), new DefaultHttpContext());

            StringAssert.Contains(result.AuthorizationUrl, Uri.EscapeDataString("http://localhost:5000/callback"));
        }

        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_AllowedTenantsHoldingThePersonalTenant_Throws()
        {
            AssertRejected(MicrosoftAuthHarness.CreateOptions(allowedTenantIds: [FakeMicrosoftIdentityProvider.PersonalAccountsTenant]), "personal");
        }

        [TestMethod]
        [DataRow("http://login.example")]
        [DataRow("https://login.example/?x=1")]
        [DataRow("login.example")]
        public void AddEasyReasyMicrosoftAuth_AuthorityThatIsHttpHasAQueryOrIsRelative_Throws(string authorityBaseUrl)
        {
            AssertRejected(MicrosoftAuthHarness.CreateOptions(authorityBaseUrl: authorityBaseUrl), nameof(MicrosoftAuthOptions.AuthorityBaseUrl));
        }

        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_StateLifetimeNotPositive_Throws()
        {
            AssertRejected(MicrosoftAuthHarness.CreateOptions(stateLifetime: TimeSpan.Zero), nameof(MicrosoftAuthOptions.StateLifetime));
        }

        [TestMethod]
        public void AddEasyReasyMicrosoftAuth_SeveralProblems_ReportsEveryOneInOneException()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new ServiceCollection().AddEasyReasyMicrosoftAuth(
                MicrosoftAuthHarness.CreateOptions(clientId: "", stateKey: new byte[1], redirectUris: [], stateLifetime: TimeSpan.Zero)));

            StringAssert.Contains(exception.Message, "client id");
            StringAssert.Contains(exception.Message, nameof(MicrosoftAuthOptions.StateKey));
            StringAssert.Contains(exception.Message, "redirect URI");
            StringAssert.Contains(exception.Message, nameof(MicrosoftAuthOptions.StateLifetime));
        }

        private static void AssertRejected(MicrosoftAuthOptions options, string expectedInMessage)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new ServiceCollection().AddEasyReasyMicrosoftAuth(options));
            StringAssert.Contains(exception.Message, expectedInMessage);
        }
    }
}
