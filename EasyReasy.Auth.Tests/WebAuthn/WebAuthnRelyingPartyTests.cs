namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class WebAuthnRelyingPartyTests
    {
        // SHA-256 of the ASCII string "example.com", the value an authenticator scoped to that relying
        // party puts in the first 32 bytes of its authenticator data. Hardcoded on purpose: computing it
        // with SHA256.HashData here would make IdHash_IsSha256OfTheIdAndNotOfTheName tautological.
        private const string ExampleComIdHashHex = "a379a6f6eeafb9a55e378c118034e2751e682fab9f2d30ab13d2125586ce1947";

        [TestMethod]
        public void Constructor_ValidConfiguration_KeepsIdNameAndOrigins()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("example.com", "Contoso", new[] { "https://example.com" });

            Assert.AreEqual("example.com", relyingParty.Id);
            Assert.AreEqual("Contoso", relyingParty.Name);
            CollectionAssert.AreEquivalent(new[] { "https://example.com" }, relyingParty.Origins.ToArray());
        }

        [TestMethod]
        public void Constructor_MixedCaseId_LowerCasesIt()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("Example.COM", "Contoso", new[] { "https://example.com" });

            Assert.AreEqual("example.com", relyingParty.Id);
        }

        [TestMethod]
        public void Constructor_PaddedValues_TrimsIdAndName()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("  example.com  ", "  Contoso  ", new[] { "  https://example.com  " });

            Assert.AreEqual("example.com", relyingParty.Id);
            Assert.AreEqual("Contoso", relyingParty.Name);
            Assert.AreEqual("https://example.com", relyingParty.Origins.Single());
        }

        [TestMethod]
        public void IdHash_IsSha256OfTheIdAndNotOfTheName()
        {
            // The name is deliberately different from the id: hashing the wrong one produces a different
            // value, so this vector fails if the two are ever transposed.
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("example.com", "Contoso", new[] { "https://example.com" });

            Assert.AreEqual(ExampleComIdHashHex, Convert.ToHexStringLower(relyingParty.IdHash.Span));
        }

        [TestMethod]
        public void Constructor_OriginsWithDefaultPortAndTrailingSlash_NormalizeToOneOrigin()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty(
                "example.com",
                "Contoso",
                new[] { "https://example.com", "https://example.com:443", "HTTPS://EXAMPLE.COM/" });

            Assert.AreEqual(1, relyingParty.Origins.Count);
            Assert.AreEqual("https://example.com", relyingParty.Origins.Single());
        }

        [TestMethod]
        public void Constructor_NonDefaultPort_KeepsThePortInTheOrigin()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("localhost", "Dev", new[] { "https://localhost:5173" });

            Assert.AreEqual("https://localhost:5173", relyingParty.Origins.Single());
        }

        [TestMethod]
        public void Constructor_SiblingSubdomainsUnderTheId_KeepsThemAll()
        {
            // The reason Origins is a set: one relying party legitimately serves several subdomains, all of
            // which share the RP ID their credentials are scoped to.
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty(
                "example.com",
                "Contoso",
                new[] { "https://app.example.com", "https://admin.example.com" });

            CollectionAssert.AreEquivalent(new[] { "https://app.example.com", "https://admin.example.com" }, relyingParty.Origins.ToArray());
        }

        [TestMethod]
        public void Constructor_SameHostOnDifferentSchemesAndPorts_KeepsThemAll()
        {
            // The other reason: a development deployment serves the same host several ways.
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty(
                "localhost",
                "Dev",
                new[] { "http://localhost:3000", "https://localhost:5173" });

            CollectionAssert.AreEquivalent(new[] { "http://localhost:3000", "https://localhost:5173" }, relyingParty.Origins.ToArray());
        }

        [DataTestMethod]
        [DataRow("https://localhost:5173")]
        [DataRow("https://contoso.example.org")]
        [DataRow("https://notexample.com")]
        [DataRow("https://example.com.evil.net")]
        public void Constructor_OriginNotUnderTheRelyingPartyId_Throws(string origin)
        {
            // A browser answers this pairing with a SecurityError, so accepting it at construction would
            // ship a deployment that can never complete a ceremony.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRelyingParty("example.com", "Contoso", new[] { origin }));

            StringAssert.Contains(exception.Message, "nor a subdomain of it");
        }

        [TestMethod]
        public void Constructor_DeeplyNestedSubdomainOfTheId_IsAccepted()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("example.com", "Contoso", new[] { "https://a.b.example.com" });

            Assert.AreEqual("https://a.b.example.com", relyingParty.Origins.Single());
        }

        [TestMethod]
        public void Constructor_NoOrigins_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRelyingParty("example.com", "Contoso", Array.Empty<string>()));

            StringAssert.Contains(exception.Message, "at least one allowed origin");
        }

        [DataTestMethod]
        [DataRow("https://example.com/app")]
        [DataRow("https://example.com?query=1")]
        [DataRow("https://example.com#fragment")]
        [DataRow("https://user@example.com")]
        [DataRow("https://evil.com@example.com")]
        [DataRow("ftp://example.com")]
        [DataRow("example.com")]
        [DataRow("")]
        [DataRow("   ")]
        public void Constructor_OriginThatIsNotAnOrigin_Throws(string origin)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRelyingParty("example.com", "Contoso", new[] { origin }));

            StringAssert.Contains(exception.Message, "not an absolute http(s) origin");
        }

        [DataTestMethod]
        [DataRow("https://example.com")]
        [DataRow("example.com:443")]
        [DataRow("example.com/path")]
        [DataRow("exa mple.com")]
        public void Constructor_IdThatIsNotABareDomain_Throws(string id)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRelyingParty(id, "Contoso", new[] { "https://example.com" }));

            StringAssert.Contains(exception.Message, "bare registrable domain");
        }

        [TestMethod]
        public void Constructor_EmptyId_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRelyingParty("   ", "Contoso", new[] { "https://example.com" }));

            StringAssert.Contains(exception.Message, "non-empty relying-party id");
        }

        [TestMethod]
        public void Constructor_EmptyName_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRelyingParty("example.com", "   ", new[] { "https://example.com" }));

            StringAssert.Contains(exception.Message, "non-empty relying-party name");
        }

        [TestMethod]
        public void Constructor_InternationalizedId_IsStoredAsTheAsciiFormABrowserUses()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("exämple.com", "Contoso", new[] { "https://exämple.com" });

            Assert.AreEqual("xn--exmple-cua.com", relyingParty.Id);
        }

        [TestMethod]
        public void IsAllowedOrigin_InternationalizedOriginPresentedAsPunycode_IsAllowed()
        {
            // A browser serializes the origin with the host in punycode, so an origin configured in Unicode
            // has to normalize to the same thing or it would never match anything.
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("exämple.com", "Contoso", new[] { "https://exämple.com" });

            Assert.IsTrue(relyingParty.IsAllowedOrigin("https://xn--exmple-cua.com"));
        }

        [TestMethod]
        public void Constructor_SeveralProblems_ReportsAllOfThemInOneMessage()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRelyingParty("https://example.com", "", new[] { "not-an-origin" }));

            StringAssert.Contains(exception.Message, "bare registrable domain");
            StringAssert.Contains(exception.Message, "non-empty relying-party name");
            StringAssert.Contains(exception.Message, "not an absolute http(s) origin");
        }

        [TestMethod]
        public void Constructor_SeveralProblems_PutsEachOnItsOwnLine()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnRelyingParty("https://example.com", "", new[] { "not-an-origin" }));

            Assert.AreEqual(3, exception.Message.Split('\n').Count(line => line.StartsWith('\'')));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void Constructor_NullArgument_Throws(int nullArgumentPosition)
        {
            string? id = nullArgumentPosition == 0 ? null : "example.com";
            string? name = nullArgumentPosition == 1 ? null : "Contoso";
            string[]? origins = nullArgumentPosition == 2 ? null : new[] { "https://example.com" };

            Assert.ThrowsException<ArgumentNullException>(() => new WebAuthnRelyingParty(id!, name!, origins!));
        }

        [TestMethod]
        public void IsAllowedOrigin_ConfiguredOrigin_IsAllowed()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("example.com", "Contoso", new[] { "https://example.com" });

            Assert.IsTrue(relyingParty.IsAllowedOrigin("https://example.com"));
        }

        [DataTestMethod]
        [DataRow("https://evil.com")]
        [DataRow("http://example.com")]
        [DataRow("https://example.com:8443")]
        [DataRow("https://sub.example.com")]
        [DataRow("not-an-origin")]
        [DataRow("")]
        public void IsAllowedOrigin_AnyOtherOrigin_IsNotAllowed(string origin)
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("example.com", "Contoso", new[] { "https://example.com" });

            Assert.IsFalse(relyingParty.IsAllowedOrigin(origin));
        }

        [TestMethod]
        public void IsAllowedOrigin_SecondConfiguredOrigin_IsAllowed()
        {
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty(
                "example.com",
                "Contoso",
                new[] { "https://app.example.com", "https://admin.example.com" });

            Assert.IsTrue(relyingParty.IsAllowedOrigin("https://admin.example.com"));
        }
    }
}
