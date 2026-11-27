using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The real flow against Keycloak: Begin's authorize URL with PKCE, Keycloak's login, and its redirect through the
    /// code redemption and the ID token's validation against its published keys, with the real HTTP client and clock. The
    /// fake issuer covers the hostile cases; this covers what a real provider actually sends, and that it refuses a second
    /// redemption of a code, which the stateless flow relies on. Runs in the <c>keycloak</c> CI job against a Keycloak
    /// container (see <see cref="KeycloakServer"/>), and is inconclusive where <c>KEYCLOAK_URL</c> is not set.
    /// </summary>
    [TestClass]
    public class OpenIdConnectKeycloakIntegrationTests
    {
        private const string KeycloakKey = "keycloak";

        private static readonly string RedirectUri = OpenIdConnectAuthHarness.SigningRedirectUri(KeycloakKey);

        private static KeycloakRealm? _realm;

        private static KeycloakRealm Realm => _realm ?? throw new InvalidOperationException("The class initializer creates the realm.");

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            if (!KeycloakAdmin.IsConfigured)
            {
                Assert.Inconclusive("KEYCLOAK_URL is not set; CLAUDE.md says how to start Keycloak for these tests.");
            }

            _realm = await KeycloakServer.CreateRealmAsync(RedirectUri);
        }

        [TestMethod]
        public async Task Complete_SignedInAtKeycloak_ReturnsTheRealmsIdentityForTheUser()
        {
            using OpenIdConnectServiceDriver driver = CreateDriver();
            FakeBrowser browser = new FakeBrowser(TimeProvider.System);
            using KeycloakBrowser keycloak = new KeycloakBrowser();

            OpenIdConnectAuthorizationResponse response = await keycloak.SignInAsync((await BeginAsync(driver, browser)).AuthorizationUrl, RedirectUri);
            OpenIdConnectAuthCompletionResult result = await driver.CompleteAsync(response, browser, OpenIdConnectAuthHarness.CallerBinding);

            Assert.IsTrue(result.Success, result.FailureReason?.ToString());
            Assert.AreEqual(new OpenIdConnectIdentity(Realm.Issuer, Realm.UserId), result.UserInfo.Identity);
            Assert.AreEqual(KeycloakKey, result.UserInfo.ProviderKey);
            Assert.AreEqual(KeycloakServer.Email, result.UserInfo.Email);
            Assert.IsTrue(result.UserInfo.EmailVerified);
            Assert.IsNotNull(result.UserInfo.AuthenticationTime);
            Assert.AreEqual(Realm.Issuer, response.Issuer);
            Assert.AreEqual(0, OpenIdConnectAuthHarness.CountBindingCookies(browser));
            // Keycloak publishes its signing certificate, so the returned key carries it for checking the token later.
            using JsonDocument key = JsonDocument.Parse(result.IdTokenSigningKey);
            Assert.AreEqual("RSA", key.RootElement.GetProperty("kty").GetString());
            Assert.IsTrue(key.RootElement.GetProperty("x5c").GetArrayLength() > 0, result.IdTokenSigningKey);
        }

        [TestMethod]
        public async Task Complete_SameCodeRedeemedASecondTime_IsRefusedByKeycloakAsCodeRedemptionFailed()
        {
            using OpenIdConnectServiceDriver driver = CreateDriver();
            FakeBrowser browser = new FakeBrowser(TimeProvider.System);
            using KeycloakBrowser keycloak = new KeycloakBrowser();
            OpenIdConnectAuthorizationResponse response = await keycloak.SignInAsync((await BeginAsync(driver, browser)).AuthorizationUrl, RedirectUri);

            // Someone who copied both the browser's binding cookie and the redirect, so only the provider stands in the
            // way: the flow keeps no record of used states or codes.
            FakeBrowser copy = browser.Clone();
            OpenIdConnectAuthCompletionResult first = await driver.CompleteAsync(response, browser, OpenIdConnectAuthHarness.CallerBinding);
            OpenIdConnectAuthCompletionResult second = await driver.CompleteAsync(response, copy, OpenIdConnectAuthHarness.CallerBinding);

            Assert.IsTrue(first.Success, first.FailureReason?.ToString());
            Assert.AreEqual(OpenIdConnectAuthFailureReason.CodeRedemptionFailed, second.FailureReason);
        }

        /// <summary>
        /// The package registered as an application registers it, with the realm as its one provider, over the real
        /// network and clock.
        /// </summary>
        private static OpenIdConnectServiceDriver CreateDriver()
        {
            FakeProviderResolver resolver = new FakeProviderResolver();
            resolver.Providers[KeycloakKey] = new OpenIdConnectProvider(
                OpenIdConnectIssuer.Fixed(Realm.Issuer),
                KeycloakServer.ClientId,
                Realm.ClientSecret,
                [RedirectUri]);

            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyOpenIdConnectAuth(OpenIdConnectAuthHarness.CreateOptions());
            services.AddSingleton<IOpenIdConnectProviderResolver>(resolver);
            return new OpenIdConnectServiceDriver(services.BuildServiceProvider());
        }

        private static Task<OpenIdConnectAuthBeginResult> BeginAsync(OpenIdConnectServiceDriver driver, FakeBrowser browser)
        {
            return driver.BeginAsync(OpenIdConnectAuthHarness.CreateRequest(KeycloakKey), browser);
        }
    }
}
