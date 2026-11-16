using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the passkey sign-in options write. Run under a hostile naming policy, so only pinned field
    /// names pass.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class WebAuthnDiscoverableOptionsJsonContractTests : HostileNamingPolicyTestBase
    {
        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_AuthenticatorSelection_RequiresAResidentKeyAndUserVerification()
        {
            using JsonDocument document = JsonDocument.Parse(DiscoverableRegistrationJson());
            JsonElement selection = document.RootElement.GetProperty("authenticatorSelection");

            CollectionAssert.AreEquivalent(new[] { "residentKey", "requireResidentKey", "userVerification" }, WebAuthnOptionsTestData.PropertyNames(selection));
            Assert.AreEqual("required", selection.GetProperty("residentKey").GetString());
            Assert.IsTrue(selection.GetProperty("requireResidentKey").GetBoolean());
            Assert.AreEqual("required", selection.GetProperty("userVerification").GetString());
        }

        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_CarryExactlyTheFieldsTheBrowserReads()
        {
            using JsonDocument document = JsonDocument.Parse(DiscoverableRegistrationJson());

            CollectionAssert.AreEquivalent(
                new[] { "rp", "user", "challenge", "pubKeyCredParams", "timeout", "excludeCredentials", "authenticatorSelection", "attestation" },
                WebAuthnOptionsTestData.PropertyNames(document.RootElement));
            Assert.AreEqual("none", document.RootElement.GetProperty("attestation").GetString());
        }

        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_ExcludedCredentials_CarryTheirTransports()
        {
            using JsonDocument document = JsonDocument.Parse(DiscoverableRegistrationJson());
            JsonElement descriptor = document.RootElement.GetProperty("excludeCredentials")[0];

            Assert.AreEqual(WebAuthnOptionsTestData.FirstCredentialId, descriptor.GetProperty("id").GetString());
            CollectionAssert.AreEqual(
                new[] { "internal" },
                descriptor.GetProperty("transports").EnumerateArray().Select(transport => transport.GetString()).ToArray());
        }

        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_NoExcludedCredentials_LeavesTheFieldOut()
        {
            string json = WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                Array.Empty<WebAuthnCredentialReference>()).ToJson();

            using JsonDocument document = JsonDocument.Parse(json);

            Assert.IsFalse(document.RootElement.TryGetProperty("excludeCredentials", out _), json);
        }

        [TestMethod]
        public void CreateDiscoverableRegistrationOptions_AuthenticatorAttachment_IsWritten()
        {
            string json = WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                Array.Empty<WebAuthnCredentialReference>(),
                WebAuthnAuthenticatorAttachment.Platform).ToJson();

            StringAssert.Contains(json, "\"authenticatorAttachment\":\"platform\"");
        }

        [TestMethod]
        public void CreateDiscoverableAuthenticationOptions_WritesAnEmptyAllowListAndRequiredUserVerification()
        {
            string json = WebAuthnTestData.NewGenerator().CreateDiscoverableAuthenticationOptions().ToJson();

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            CollectionAssert.AreEquivalent(
                new[] { "challenge", "timeout", "rpId", "allowCredentials", "userVerification" },
                WebAuthnOptionsTestData.PropertyNames(root));
            Assert.AreEqual(JsonValueKind.Array, root.GetProperty("allowCredentials").ValueKind);
            Assert.AreEqual(0, root.GetProperty("allowCredentials").GetArrayLength());
            Assert.AreEqual("required", root.GetProperty("userVerification").GetString());
            Assert.AreEqual(WebAuthnTestData.RelyingPartyId, root.GetProperty("rpId").GetString());
        }

        [TestMethod]
        public void CreateDiscoverableAuthenticationOptions_CarryTheChallengeThatWasIssuedAndTheTimeout()
        {
            PublicKeyCredentialRequestOptions options = WebAuthnTestData.NewGenerator().CreateDiscoverableAuthenticationOptions(30_000);

            using JsonDocument document = JsonDocument.Parse(options.ToJson());

            Assert.AreEqual(options.Challenge, document.RootElement.GetProperty("challenge").GetString());
            Assert.AreEqual(30_000, document.RootElement.GetProperty("timeout").GetInt32());
            byte[]? challengeBytes = Base64UrlEncoding.TryDecodeCanonical(options.Challenge);
            Assert.IsNotNull(challengeBytes);
            Assert.AreEqual(WebAuthnOptionsGenerator.ChallengeSizeInBytes, challengeBytes.Length);
        }

        private static string DiscoverableRegistrationJson()
        {
            return WebAuthnTestData.NewGenerator().CreateDiscoverableRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                new[] { new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, new[] { "internal" }) }).ToJson();
        }
    }
}
