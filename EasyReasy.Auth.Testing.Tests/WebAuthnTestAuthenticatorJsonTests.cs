using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// The JSON forms: options as an endpoint returns them go in, and the body a page posts comes out and
    /// parses with the library's <c>FromJson</c>.
    /// </summary>
    [TestClass]
    public class WebAuthnTestAuthenticatorJsonTests
    {
        [TestMethod]
        public void Register_OptionsAsJson_BodyParsesWithFromJsonAndVerifies()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator().CreateDiscoverableRegistrationOptions(
                TestRelyingParty.NewUser(),
                Array.Empty<WebAuthnCredentialReference>());

            WebAuthnTestRegistration registration = authenticator.Register(options.ToJson(), TestRelyingParty.Origin);
            WebAuthnRegistrationResponse parsed = WebAuthnRegistrationResponse.FromJson(registration.Json);

            WebAuthnRegistrationResult result = TestRelyingParty.NewVerifier().VerifyDiscoverableRegistration(parsed, options.Challenge);
            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
            Assert.AreEqual(registration.Response.ToJson(), parsed.ToJson());
        }

        [TestMethod]
        public void Assert_OptionsAsJson_BodyParsesWithFromJsonAndVerifies()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            WebAuthnStoredCredential stored = TestRelyingParty.Stored(TestRelyingParty.RegisterPasskey(authenticator));
            PublicKeyCredentialRequestOptions options = TestRelyingParty.NewGenerator().CreateDiscoverableAuthenticationOptions();

            WebAuthnTestAssertion assertion = authenticator.Assert(options.ToJson(), TestRelyingParty.Origin);
            WebAuthnAuthenticationResponse parsed = WebAuthnAuthenticationResponse.FromJson(assertion.Json);

            WebAuthnAuthenticationResult result = TestRelyingParty.NewVerifier().VerifyDiscoverableAuthentication(
                parsed,
                options.Challenge,
                stored,
                TestRelyingParty.UserHandle);
            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
            Assert.AreEqual(assertion.Response.ToJson(), parsed.ToJson());
        }

        [TestMethod]
        public void Register_Json_CarriesTheMembersTheBrowserWrites()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator().CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred);

            JsonObject body = ParseObject(authenticator.Register(options, TestRelyingParty.Origin).Json);
            JsonObject response = body["response"]?.AsObject() ?? throw new AssertFailedException("No response member.");

            Assert.AreEqual("public-key", (string?)body["type"]);
            Assert.AreEqual(0, body["clientExtensionResults"]?.AsObject().Count);
            Assert.AreEqual((int)CoseAlgorithm.Es256, (int?)response["publicKeyAlgorithm"]);
            Assert.AreEqual("internal", (string?)response["transports"]?[0]);
        }

        [TestMethod]
        public void Register_Json_AuthenticatorDataIsTheOneInsideTheAttestationObject()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            JsonObject response = RegistrationResponseMember(authenticator);
            byte[] attestationObject = Base64Url.DecodeFromChars(ReadString(response, "attestationObject"));

            CollectionAssert.AreEqual(ReadAuthData(attestationObject), Base64Url.DecodeFromChars(ReadString(response, "authenticatorData")));
        }

        [TestMethod]
        public void Register_Json_PublicKeyIsTheEnrolledKey()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            JsonObject response = RegistrationResponseMember(authenticator);
            using ECDsa publicKey = ECDsa.Create();
            publicKey.ImportSubjectPublicKeyInfo(Base64Url.DecodeFromChars(ReadString(response, "publicKey")), out int _);

            ECParameters expected = CosePublicKeyReader.ReadEs256(authenticator.CosePublicKey);
            ECParameters actual = publicKey.ExportParameters(false);
            CollectionAssert.AreEqual(expected.Q.X, actual.Q.X);
            CollectionAssert.AreEqual(expected.Q.Y, actual.Q.Y);
        }

        [TestMethod]
        public void Assert_Json_CarriesClientExtensionResults()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            TestRelyingParty.RegisterPasskey(authenticator);

            JsonObject body = ParseObject(authenticator.Assert(TestRelyingParty.NewGenerator().CreateDiscoverableAuthenticationOptions(), TestRelyingParty.Origin).Json);

            Assert.AreEqual(0, body["clientExtensionResults"]?.AsObject().Count);
        }

        [TestMethod]
        public void RegisterAndAssert_OptionsNamingNoRpId_UseTheOriginsHostAsTheBrowserDoes()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            PublicKeyCredentialCreationOptions creationOptions = TestRelyingParty.NewGenerator().CreateDiscoverableRegistrationOptions(
                TestRelyingParty.NewUser(),
                Array.Empty<WebAuthnCredentialReference>());
            JsonObject creationJson = ParseObject(creationOptions.ToJson());
            creationJson["rp"]?.AsObject().Remove("id");

            WebAuthnTestRegistration registration = authenticator.Register(creationJson.ToJsonString(), TestRelyingParty.Origin);
            WebAuthnRegistrationResult registered = TestRelyingParty.NewVerifier().VerifyDiscoverableRegistration(registration.Response, creationOptions.Challenge);
            Assert.IsTrue(registered.Success, $"{registered.FailureReason}: {registered.FailureMessage}");

            PublicKeyCredentialRequestOptions requestOptions = TestRelyingParty.NewGenerator().CreateDiscoverableAuthenticationOptions();
            JsonObject requestJson = ParseObject(requestOptions.ToJson());
            requestJson.Remove("rpId");

            WebAuthnTestAssertion assertion = authenticator.Assert(requestJson.ToJsonString(), TestRelyingParty.Origin);
            WebAuthnAuthenticationResult result = TestRelyingParty.NewVerifier().VerifyDiscoverableAuthentication(
                assertion.Response,
                requestOptions.Challenge,
                TestRelyingParty.Stored(registered.Credential ?? throw new AssertFailedException("No credential.")),
                TestRelyingParty.UserHandle);
            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
        }

        private static JsonObject RegistrationResponseMember(WebAuthnTestAuthenticator authenticator)
        {
            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator().CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred);
            JsonObject body = ParseObject(authenticator.Register(options, TestRelyingParty.Origin).Json);

            return body["response"]?.AsObject() ?? throw new AssertFailedException("No response member.");
        }

        private static string ReadString(JsonObject parent, string name)
        {
            return (string?)parent[name] ?? throw new AssertFailedException($"No \"{name}\" string.");
        }

        /// <summary>Reads the <c>authData</c> byte string out of an attestation object.</summary>
        private static byte[] ReadAuthData(byte[] attestationObject)
        {
            CborReader reader = new CborReader(attestationObject);
            int? entryCount = reader.ReadStartMap();

            for (int index = 0; index < entryCount; index++)
            {
                if (reader.ReadTextString() == "authData")
                {
                    return reader.ReadByteString();
                }

                reader.SkipValue();
            }

            throw new AssertFailedException("The attestation object has no authData.");
        }

        private static JsonObject ParseObject(string json)
        {
            return JsonNode.Parse(json)?.AsObject() ?? throw new AssertFailedException("The JSON is not an object.");
        }
    }
}
