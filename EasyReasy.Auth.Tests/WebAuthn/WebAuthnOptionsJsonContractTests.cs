using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The JSON these options serialize to is read by a browser, so its field names and value spellings are
    /// fixed by WebAuthn rather than by whatever serializer settings the application happens to hold.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class WebAuthnOptionsJsonContractTests : HostileNamingPolicyTestBase
    {
        [TestMethod]
        public void CreationOptions_CarryExactlyTheFieldsTheBrowserReads()
        {
            using JsonDocument document = JsonDocument.Parse(RegistrationJson());

            CollectionAssert.AreEquivalent(
                new[] { "rp", "user", "challenge", "pubKeyCredParams", "timeout", "excludeCredentials", "authenticatorSelection", "attestation" },
                PropertyNames(document.RootElement));
        }

        [TestMethod]
        public void CreationOptions_RelyingPartyCarriesExactlyItsSpecFields()
        {
            using JsonDocument document = JsonDocument.Parse(RegistrationJson());
            JsonElement relyingParty = document.RootElement.GetProperty("rp");

            CollectionAssert.AreEquivalent(new[] { "id", "name" }, PropertyNames(relyingParty));
            Assert.AreEqual("example.com", relyingParty.GetProperty("id").GetString());
            Assert.AreEqual("Contoso", relyingParty.GetProperty("name").GetString());
        }

        [TestMethod]
        public void CreationOptions_UserCarriesExactlyItsSpecFields()
        {
            using JsonDocument document = JsonDocument.Parse(RegistrationJson());
            JsonElement user = document.RootElement.GetProperty("user");

            CollectionAssert.AreEquivalent(new[] { "id", "name", "displayName" }, PropertyNames(user));
            Assert.AreEqual(WebAuthnOptionsTestData.UserName, user.GetProperty("name").GetString());
            Assert.AreEqual(WebAuthnOptionsTestData.UserDisplayName, user.GetProperty("displayName").GetString());
        }

        [TestMethod]
        public void CreationOptions_AuthenticatorSelectionCarriesExactlyItsSpecFields()
        {
            using JsonDocument document = JsonDocument.Parse(RegistrationJson());
            JsonElement selection = document.RootElement.GetProperty("authenticatorSelection");

            CollectionAssert.AreEquivalent(new[] { "residentKey", "requireResidentKey", "userVerification" }, PropertyNames(selection));
            Assert.AreEqual("discouraged", selection.GetProperty("residentKey").GetString());
            Assert.IsFalse(selection.GetProperty("requireResidentKey").GetBoolean());
        }

        [TestMethod]
        public void CreationOptions_CredentialDescriptorsCarryExactlyTheirSpecFields()
        {
            using JsonDocument document = JsonDocument.Parse(RegistrationJson());
            JsonElement descriptor = document.RootElement.GetProperty("excludeCredentials")[0];

            CollectionAssert.AreEquivalent(new[] { "type", "id" }, PropertyNames(descriptor));
            Assert.AreEqual("public-key", descriptor.GetProperty("type").GetString());
            Assert.AreEqual(WebAuthnOptionsTestData.FirstCredentialId, descriptor.GetProperty("id").GetString());
        }

        [TestMethod]
        public void CreationOptions_AlgorithmParametersCarryExactlyTheirSpecFields()
        {
            using JsonDocument document = JsonDocument.Parse(RegistrationJson());
            JsonElement parameters = document.RootElement.GetProperty("pubKeyCredParams")[0];

            CollectionAssert.AreEquivalent(new[] { "type", "alg" }, PropertyNames(parameters));
            Assert.AreEqual(-7, parameters.GetProperty("alg").GetInt32());
        }

        [TestMethod]
        public void CreationOptions_CarryTheChallengeThatWasIssued()
        {
            PublicKeyCredentialCreationOptions options = NewRegistrationOptions();

            using JsonDocument document = JsonDocument.Parse(options.ToJson());

            Assert.AreEqual(options.Challenge, document.RootElement.GetProperty("challenge").GetString());
        }

        [TestMethod]
        public void CreationOptions_AttestationIsNone()
        {
            using JsonDocument document = JsonDocument.Parse(RegistrationJson());

            Assert.AreEqual("none", document.RootElement.GetProperty("attestation").GetString());
        }

        [TestMethod]
        public void RequestOptions_CarryExactlyTheFieldsTheBrowserReads()
        {
            using JsonDocument document = JsonDocument.Parse(AuthenticationJson());

            CollectionAssert.AreEquivalent(
                new[] { "challenge", "timeout", "rpId", "allowCredentials", "userVerification" },
                PropertyNames(document.RootElement));
        }

        [TestMethod]
        public void RequestOptions_CarryTheRelyingPartyIdAndTheChallengeThatWasIssued()
        {
            PublicKeyCredentialRequestOptions options = NewAuthenticationOptions();

            using JsonDocument document = JsonDocument.Parse(options.ToJson());

            Assert.AreEqual("example.com", document.RootElement.GetProperty("rpId").GetString());
            Assert.AreEqual(options.Challenge, document.RootElement.GetProperty("challenge").GetString());
        }

        [TestMethod]
        public void RequestOptions_CredentialDescriptorsCarryExactlyTheirSpecFields()
        {
            using JsonDocument document = JsonDocument.Parse(AuthenticationJson());
            JsonElement descriptor = document.RootElement.GetProperty("allowCredentials")[0];

            CollectionAssert.AreEquivalent(new[] { "type", "id" }, PropertyNames(descriptor));
            Assert.AreEqual(WebAuthnOptionsTestData.FirstCredentialId, descriptor.GetProperty("id").GetString());
        }

        [DataTestMethod]
        [DataRow(WebAuthnUserVerificationRequirement.Required, "required")]
        [DataRow(WebAuthnUserVerificationRequirement.Preferred, "preferred")]
        [DataRow(WebAuthnUserVerificationRequirement.Discouraged, "discouraged")]
        public void UserVerification_IsWrittenWithTheSpecSpelling(WebAuthnUserVerificationRequirement userVerification, string expected)
        {
            string json = WebAuthnTestData.NewGenerator()
                .CreateAuthenticationOptions(new[] { WebAuthnOptionsTestData.FirstCredentialId }, userVerification)
                .ToJson();

            StringAssert.Contains(json, $"\"userVerification\":\"{expected}\"");
        }

        [DataTestMethod]
        [DataRow(WebAuthnAuthenticatorAttachment.Platform, "platform")]
        [DataRow(WebAuthnAuthenticatorAttachment.CrossPlatform, "cross-platform")]
        public void AuthenticatorAttachment_IsWrittenWithTheSpecSpelling(WebAuthnAuthenticatorAttachment attachment, string expected)
        {
            // "cross-platform" is not what any naming policy produces from CrossPlatform, so this is the
            // spelling most likely to drift.
            string json = WebAuthnTestData.NewGenerator().CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                authenticatorAttachment: attachment).ToJson();

            StringAssert.Contains(json, $"\"authenticatorAttachment\":\"{expected}\"");
        }

        [TestMethod]
        public void CreationOptions_ToString_ReturnsTheSameStringAsToJson()
        {
            PublicKeyCredentialCreationOptions options = NewRegistrationOptions();

            Assert.AreEqual(options.ToJson(), options.ToString());
        }

        [TestMethod]
        public void RequestOptions_ToString_ReturnsTheSameStringAsToJson()
        {
            PublicKeyCredentialRequestOptions options = NewAuthenticationOptions();

            Assert.AreEqual(options.ToJson(), options.ToString());
        }

        private static PublicKeyCredentialCreationOptions NewRegistrationOptions()
        {
            return WebAuthnTestData.NewGenerator().CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                excludeCredentialIds: new[] { WebAuthnOptionsTestData.FirstCredentialId });
        }

        private static PublicKeyCredentialRequestOptions NewAuthenticationOptions()
        {
            return WebAuthnTestData.NewGenerator().CreateAuthenticationOptions(
                new[] { WebAuthnOptionsTestData.FirstCredentialId },
                WebAuthnUserVerificationRequirement.Required);
        }

        private static string RegistrationJson()
        {
            return NewRegistrationOptions().ToJson();
        }

        private static string AuthenticationJson()
        {
            return NewAuthenticationOptions().ToJson();
        }

        private static string[] PropertyNames(JsonElement element)
        {
            return element.EnumerateObject().Select(property => property.Name).ToArray();
        }
    }
}
