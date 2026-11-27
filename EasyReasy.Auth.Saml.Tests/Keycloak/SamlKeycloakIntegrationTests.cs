using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The real flow against Keycloak: a signed AuthnRequest over HTTP-Redirect, Keycloak's login, and its signed (and,
    /// for one realm, encrypted) response through the assertion consumer service to completion. The fake identity provider
    /// covers the hostile cases; this covers what a real identity provider actually sends, and that a realm configured
    /// below the package's rules is refused. Runs in the <c>keycloak</c> CI job against a Keycloak container (see
    /// <see cref="KeycloakServer"/>), and is inconclusive where <c>KEYCLOAK_URL</c> is not set.
    /// </summary>
    [TestClass]
    public class SamlKeycloakIntegrationTests
    {
        private const string KeycloakKey = "keycloak";

        private static KeycloakRealm _signedRealm = null!;
        private static KeycloakRealm _encryptedRealm = null!;
        private static KeycloakRealm _sha1Realm = null!;

        private RecordingLoggerProvider _logs = null!;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            if (!KeycloakAdmin.IsConfigured)
            {
                Assert.Inconclusive("KEYCLOAK_URL is not set; CLAUDE.md says how to start Keycloak for these tests.");
            }

            X509Certificate2 requestSigning = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderSigning);
            _signedRealm = await KeycloakServer.CreateRealmAsync(requestSigning);
            _encryptedRealm = await KeycloakServer.CreateRealmAsync(requestSigning, SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption));
            _sha1Realm = await KeycloakServer.CreateRealmAsync(requestSigning, signatureAlgorithm: "RSA_SHA1");
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _logs = new RecordingLoggerProvider();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Why a response was refused is logged, not returned, so a failing test shows it here.
            foreach (RecordingLoggerProvider.Entry entry in _logs.Entries.Where(entry => entry.Level >= LogLevel.Warning))
            {
                Console.WriteLine($"{entry.Level} {entry.Category}: {entry.Message} {entry.Exception}");
            }
        }

        [TestMethod]
        public async Task SignIn_ThroughKeycloak_HandsTheHandlerThePersistentSubjectAndIssuesItsAuthResponse()
        {
            await using SamlFlowHost host = await StartHostAsync(_signedRealm);
            using KeycloakBrowser keycloak = new KeycloakBrowser();
            FakeBrowser browser = new FakeBrowser(host.Clock);

            string code = await AnswerAsync(host, await keycloak.SignInAsync(await host.BeginAsync(browser, KeycloakKey)));
            HttpResponseMessage signedIn = await host.PostSignInAsync(browser, code);

            string body = await signedIn.Content.ReadAsStringAsync();
            Assert.AreEqual(HttpStatusCode.OK, signedIn.StatusCode, body);
            using JsonDocument json = JsonDocument.Parse(body);
            Assert.AreEqual(host.Handler.ResponseToReturn!.Token, json.RootElement.GetProperty("token").GetString());
            Assert.IsTrue(keycloak.LoginFormShown);
            // The default identity source takes only a persistent NameID, so the subject handed over is one.
            SamlUserInfo user = host.Handler.HandledUsers.Single();
            Assert.AreEqual(KeycloakKey, user.Identity.IdentityProviderKey);
            Assert.AreEqual(_signedRealm.EntityId, user.Identity.IdentityProviderEntityId);
            CollectionAssert.AreEqual(new[] { KeycloakServer.Username }, user.Attributes["uid"].ToArray());
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task SignIn_TwiceInOneKeycloakSession_ReusesTheSessionAndKeepsTheSubject()
        {
            await using SamlFlowHost host = await StartHostAsync(_signedRealm);
            using KeycloakBrowser keycloak = new KeycloakBrowser();

            SamlAuthCompletionResult first = await SignInAsync(host, keycloak);
            SamlAuthCompletionResult second = await SignInAsync(host, keycloak);

            Assert.IsTrue(first.Success, first.ToString());
            Assert.IsTrue(second.Success, second.ToString());
            Assert.IsFalse(keycloak.LoginFormShown, "Keycloak asked for the password again without ForceAuthn.");
            Assert.AreEqual(first.UserInfo.Identity, second.UserInfo.Identity);
        }

        [TestMethod]
        public async Task SignIn_ForceAuthenticationInAnExistingSession_AuthenticatesAgainWithinTheMaxAge()
        {
            await using SamlFlowHost host = await StartHostAsync(_signedRealm);
            using KeycloakBrowser keycloak = new KeycloakBrowser();
            SamlAuthCompletionResult first = await SignInAsync(host, keycloak);
            // AuthnInstant has whole-second precision, so the re-check's can only be later if a second has passed.
            await Task.Delay(TimeSpan.FromSeconds(1.5));

            SamlAuthCompletionResult recheck = await SignInAsync(host, keycloak, request =>
            {
                request.ForceAuthentication = true;
                request.MaxAuthenticationAge = TimeSpan.FromMinutes(1);
                request.ExpectedIdentity = first.UserInfo!.Identity;
            });

            Assert.IsTrue(recheck.Success, recheck.ToString());
            Assert.IsTrue(keycloak.LoginFormShown, "Keycloak reused its session despite ForceAuthn.");
            Assert.IsTrue(recheck.UserInfo.AuthenticationInstant > first.UserInfo!.AuthenticationInstant);
        }

        [TestMethod]
        public async Task AssertionConsumer_KeycloaksResponsePostedTwice_IsRefusedTheSecondTime()
        {
            await using SamlFlowHost host = await StartHostAsync(_signedRealm);
            using KeycloakBrowser keycloak = new KeycloakBrowser();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest(request => request.IdentityProviderKey = KeycloakKey));
            string samlResponse = await keycloak.SignInAsync(begun!.RedirectUrl);
            await AnswerAsync(host, samlResponse);

            HttpResponseMessage replay = await host.PostAssertionAsync(samlResponse);

            Assert.AreEqual(HttpStatusCode.BadRequest, replay.StatusCode);
        }

        [TestMethod]
        public async Task SignIn_EncryptedAssertion_IsDecryptedAndAccepted()
        {
            await using SamlFlowHost host = await StartHostAsync(_encryptedRealm, decrypt: true);
            using KeycloakBrowser keycloak = new KeycloakBrowser();
            FakeBrowser browser = new FakeBrowser(host.Clock);
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest(request => request.IdentityProviderKey = KeycloakKey));
            string samlResponse = await keycloak.SignInAsync(begun!.RedirectUrl);

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, await AnswerAsync(host, samlResponse));

            string responseXml = Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse));
            StringAssert.Contains(responseXml, "EncryptedAssertion");
            StringAssert.Contains(responseXml, SamlAlgorithms.Aes256Gcm);
            StringAssert.Contains(responseXml, SamlAlgorithms.RsaOaep11);
            Assert.IsTrue(result.Success, result.ToString());
        }

        [TestMethod]
        public async Task SignIn_AttributeIdentitySource_TakesTheSubjectFromTheAttribute()
        {
            await using SamlFlowHost host = await StartHostAsync(_signedRealm, identitySource: SamlIdentitySource.Attribute("uid"));
            using KeycloakBrowser keycloak = new KeycloakBrowser();

            SamlAuthCompletionResult result = await SignInAsync(host, keycloak);

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(new SamlIdentity(KeycloakKey, _signedRealm.EntityId, KeycloakServer.Username), result.UserInfo.Identity);
        }

        [TestMethod]
        public async Task SignIn_RealmSigningWithRsaSha1_IsRefusedAtCompletionAndTheLogNamesTheCheck()
        {
            await using SamlFlowHost host = await StartHostAsync(_sha1Realm);
            using KeycloakBrowser keycloak = new KeycloakBrowser();
            FakeBrowser browser = new FakeBrowser(host.Clock);

            // The assertion consumer service seals the refusal and still redirects; the completion reports it.
            string code = await AnswerAsync(host, await keycloak.SignInAsync(await host.BeginAsync(browser, KeycloakKey)));
            HttpResponseMessage signedIn = await host.PostSignInAsync(browser, code);

            Assert.AreEqual(HttpStatusCode.Unauthorized, signedIn.StatusCode);
            Assert.AreEqual(0, host.Handler.HandledUsers.Count);
            RecordingLoggerProvider.Entry refusal = _logs.Entries.Single(entry => entry.Level == LogLevel.Warning && entry.Category == typeof(SamlAuthService).FullName);
            // The refusal says neither decryption nor signature (an oracle reads that); the exception logged with it does.
            StringAssert.Contains(refusal.Exception?.InnerException?.Message, "Illegal signature method http://www.w3.org/2000/09/xmldsig#rsa-sha1");
        }

        private async Task<SamlFlowHost> StartHostAsync(KeycloakRealm realm, bool decrypt = false, SamlIdentitySource? identitySource = null)
        {
            SamlFlowHost host = await SamlFlowHost.StartAsync(
                signingCertificate: SamlTestCertificates.ServiceProviderSigning,
                decryptionCertificates: decrypt ? [SamlTestCertificates.ServiceProviderDecryption] : [],
                loggerProvider: _logs);
            host.Resolver.IdentityProviders[KeycloakKey] = new ConfiguredSamlIdentityProvider(realm.MetadataXml, identitySource);
            return host;
        }

        private static async Task<SamlAuthCompletionResult> SignInAsync(SamlFlowHost host, KeycloakBrowser keycloak, Action<SamlAuthBeginRequestBuilder>? customize = null)
        {
            FakeBrowser browser = new FakeBrowser(host.Clock);
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest(request =>
            {
                request.IdentityProviderKey = KeycloakKey;
                customize?.Invoke(request);
            }));
            Assert.IsNotNull(begun);

            string code = await AnswerAsync(host, await keycloak.SignInAsync(begun.RedirectUrl));
            return await host.ServiceCompleteAsync(browser, code);
        }

        /// <summary>
        /// Posts Keycloak's response to the assertion consumer service for its completion code, with the host's clock on
        /// real time, since Keycloak stamps the assertion's times from its own clock.
        /// </summary>
        private static async Task<string> AnswerAsync(SamlFlowHost host, string samlResponse)
        {
            host.Clock.SetUtcNow(DateTimeOffset.UtcNow);
            return await host.PostAssertionForCodeAsync(samlResponse);
        }
    }
}
