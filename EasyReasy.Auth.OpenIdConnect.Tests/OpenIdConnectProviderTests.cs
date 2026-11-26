namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The rules a provider is checked against when it is built, so no flow meets an invalid one.
    /// </summary>
    [TestClass]
    public class OpenIdConnectProviderTests
    {
        [TestMethod]
        public void Constructor_ValidValues_KeepsThemWithTheDefaults()
        {
            OpenIdConnectProvider provider = Create();

            Assert.AreEqual("https://issuer.example", provider.Issuer.Value);
            Assert.AreEqual(OpenIdConnectClientAuthentication.ClientSecretBasic, provider.ClientAuthentication);
            CollectionAssert.AreEqual(new[] { "RS256" }, provider.SigningAlgorithms.ToArray());
            Assert.AreEqual(OpenIdConnectProvider.DefaultScope, provider.Scope);
        }

        [TestMethod]
        public void Constructor_ListsChangedAfterwards_KeepsTheValuesItWasBuiltWith()
        {
            List<string> redirectUris = ["https://app.example/callback"];
            List<string> algorithms = ["RS256"];
            OpenIdConnectProvider provider = Create(redirectUris: redirectUris, signingAlgorithms: algorithms);

            redirectUris[0] = "https://evil.example/callback";
            algorithms[0] = "HS256";

            Assert.AreEqual("https://app.example/callback", provider.RedirectUris.Single());
            Assert.AreEqual("RS256", provider.SigningAlgorithms.Single());
        }

        [TestMethod]
        [DataRow("http://app.example/callback")]
        [DataRow("https://app.example/callback#fragment")]
        [DataRow("/callback")]
        public void Constructor_RedirectUriNotAnHttpsUrlWithoutFragment_ThrowsNamingIt(string redirectUri)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Create(redirectUris: [redirectUri]));

            StringAssert.Contains(exception.Message, redirectUri);
        }

        [TestMethod]
        [DataRow("http://localhost:5173/callback")]
        [DataRow("http://app.localhost:5173/callback")]
        [DataRow("http://[::1]:5173/callback")]
        public void Constructor_RedirectUriTheBrowserTreatsAsSecure_Builds(string redirectUri)
        {
            Assert.AreEqual(redirectUri, Create(redirectUris: [redirectUri]).RedirectUris.Single());
        }

        [TestMethod]
        [DataRow("HS256")]
        [DataRow("none")]
        [DataRow("RS1")]
        public void Constructor_SigningAlgorithmThatIsNotAllowedAsymmetric_ThrowsNamingIt(string algorithm)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Create(signingAlgorithms: [algorithm]));

            StringAssert.Contains(exception.Message, algorithm);
        }

        [TestMethod]
        public void Constructor_EveryAllowedAsymmetricAlgorithm_Builds()
        {
            IReadOnlyList<string> all = ["RS256", "RS384", "RS512", "PS256", "PS384", "PS512", "ES256", "ES384", "ES512"];

            Assert.AreEqual(all.Count, Create(signingAlgorithms: all).SigningAlgorithms.Count);
        }

        [TestMethod]
        public void Constructor_ScopeWithoutOpenId_ThrowsNamingIt()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new OpenIdConnectProvider(
                OpenIdConnectIssuer.Fixed("https://issuer.example"), "client", "secret", ["https://app.example/callback"], scope: "profile email openidish"));

            StringAssert.Contains(exception.Message, "openidish");
        }

        [TestMethod]
        public void Constructor_SeveralProblems_ReportsEveryOneInOneException()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new OpenIdConnectProvider(
                OpenIdConnectIssuer.Fixed("https://issuer.example"),
                " ",
                "",
                [],
                (OpenIdConnectClientAuthentication)9,
                [],
                "profile"));

            StringAssert.Contains(exception.Message, "client id");
            StringAssert.Contains(exception.Message, "client secret");
            StringAssert.Contains(exception.Message, "at least one redirect URI");
            StringAssert.Contains(exception.Message, "client authentication 9");
            StringAssert.Contains(exception.Message, "at least one signing algorithm");
            StringAssert.Contains(exception.Message, "scope 'profile'");
        }

        private static OpenIdConnectProvider Create(IReadOnlyList<string>? redirectUris = null, IReadOnlyList<string>? signingAlgorithms = null)
        {
            return new OpenIdConnectProvider(
                OpenIdConnectIssuer.Fixed("https://issuer.example"),
                "client",
                "secret",
                redirectUris ?? ["https://app.example/callback"],
                signingAlgorithms: signingAlgorithms);
        }
    }
}
