using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// The authenticator's ceremonies verify with the real <see cref="WebAuthnVerifier"/> in both modes and
    /// with both algorithms, so a consumer's test that passes is a ceremony the library accepts.
    /// </summary>
    [TestClass]
    public class WebAuthnTestAuthenticatorVerificationTests
    {
        [DataTestMethod]
        [DataRow(CoseAlgorithm.Es256)]
        [DataRow(CoseAlgorithm.Rs256)]
        public void RegisterAndAssert_SecondFactor_VerifyWithTheRealVerifier(CoseAlgorithm algorithm)
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(algorithm);

            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator, WebAuthnUserVerificationRequirement.Required);
            WebAuthnAuthenticationResult result = TestRelyingParty.SignInSecondFactor(
                authenticator,
                TestRelyingParty.Stored(credential),
                WebAuthnUserVerificationRequirement.Required);

            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
            Assert.AreEqual(WebAuthnAuthenticationMode.SecondFactor, result.Mode);
            Assert.IsTrue(result.UserVerified);
            Assert.AreEqual(algorithm, credential.Algorithm);
        }

        [DataTestMethod]
        [DataRow(CoseAlgorithm.Es256)]
        [DataRow(CoseAlgorithm.Rs256)]
        public void RegisterAndAssert_Passkey_VerifyWithTheRealVerifier(CoseAlgorithm algorithm)
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(algorithm);

            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterPasskey(authenticator);
            WebAuthnAuthenticationResult result = TestRelyingParty.SignInWithPasskey(authenticator, TestRelyingParty.Stored(credential));

            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
            Assert.AreEqual(WebAuthnAuthenticationMode.Discoverable, result.Mode);
            Assert.AreEqual(algorithm, credential.Algorithm);
        }

        [TestMethod]
        public void Register_Default_EnrollsTheAuthenticatorsCredentialAndKey()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            Assert.AreEqual(CoseAlgorithm.Es256, authenticator.Algorithm);
            CollectionAssert.AreEqual(authenticator.CredentialId, Base64Url.DecodeFromChars(credential.CredentialId));
            CollectionAssert.AreEqual(authenticator.CosePublicKey, Base64Url.DecodeFromChars(credential.PublicKey));
            Assert.AreEqual(WebAuthnTestAuthenticator.TestAaguid, credential.Aaguid);
            Assert.AreEqual(0u, credential.SignCount);
            CollectionAssert.AreEqual(new[] { "internal" }, credential.Transports?.ToArray());
        }

        [TestMethod]
        public void Register_GivenCredentialId_EnrollsThatId()
        {
            byte[] credentialId = new byte[] { 0xC0, 0xDE, 0x01, 0x02 };
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(credentialId: credentialId);

            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            Assert.AreEqual(Base64Url.EncodeToString(credentialId), credential.CredentialId);
        }

        [TestMethod]
        public void Register_LongestCredentialIdWebAuthnAllows_Verifies()
        {
            byte[] credentialId = RandomNumberGenerator.GetBytes(WebAuthnTestAuthenticator.MaximumCredentialIdLength);
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(credentialId: credentialId);

            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            Assert.AreEqual(Base64Url.EncodeToString(credentialId), credential.CredentialId);
        }

        [TestMethod]
        public void Register_OriginWithATrailingSlash_WritesTheOriginAsABrowserSerializesIt()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator().CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred);

            WebAuthnTestRegistration registration = authenticator.Register(options, "HTTPS://Example.com:443/");

            string clientData = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(registration.Response.Response.ClientDataJson));
            StringAssert.Contains(clientData, "\"origin\":\"https://example.com\"");
        }

        [TestMethod]
        public void RegisterAndAssert_InternationalizedDomainWithNoRpId_HashThePunycodeHost()
        {
            const string unicodeOrigin = "https://bücher.example";
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty("xn--bcher-kva.example", "Contoso", new[] { unicodeOrigin });
            WebAuthnOptionsGenerator generator = new WebAuthnOptionsGenerator(relyingParty);
            WebAuthnVerifier verifier = new WebAuthnVerifier(relyingParty);
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            PublicKeyCredentialCreationOptions options = generator.CreateRegistrationOptions(TestRelyingParty.NewUser(), WebAuthnUserVerificationRequirement.Preferred);
            JsonObject optionsJson = JsonNode.Parse(options.ToJson())?.AsObject()
                ?? throw new AssertFailedException("The options are not a JSON object.");
            optionsJson["rp"]?.AsObject().Remove("id");

            WebAuthnTestRegistration registration = authenticator.Register(optionsJson.ToJsonString(), unicodeOrigin);
            WebAuthnRegistrationResult result = verifier.VerifyRegistration(registration.Response, options.Challenge, WebAuthnUserVerificationRequirement.Preferred);

            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
        }

        [TestMethod]
        public void Create_TwoAuthenticatorsWithoutAnId_GetDifferentCredentialIds()
        {
            using WebAuthnTestAuthenticator first = WebAuthnTestAuthenticator.Create();
            using WebAuthnTestAuthenticator second = WebAuthnTestAuthenticator.Create();

            Assert.AreEqual(WebAuthnTestAuthenticator.DefaultCredentialIdLength, first.CredentialId.Length);
            CollectionAssert.AreNotEqual(first.CredentialId, second.CredentialId);
        }

        [TestMethod]
        public void Register_BackupFlagsAndTransportsSet_AreWhatTheServerStores()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.BackupEligible = true;
            authenticator.BackedUp = true;
            authenticator.Transports = new[] { "hybrid", "internal" };

            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            Assert.IsTrue(credential.BackupEligible);
            Assert.IsTrue(credential.BackedUp);
            CollectionAssert.AreEqual(new[] { "hybrid", "internal" }, credential.Transports?.ToArray());
        }

        [TestMethod]
        public void Assert_BackedUpSet_IsReportedOnTheResult()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.BackupEligible = true;
            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);
            authenticator.BackedUp = true;

            WebAuthnAuthenticationResult result = TestRelyingParty.SignInSecondFactor(authenticator, TestRelyingParty.Stored(credential));

            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
            Assert.IsTrue(result.BackedUp);
        }

        [TestMethod]
        public void Sign_Es256_VerifiesUnderTheCosePublicKeyAsADerSequence()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(CoseAlgorithm.Es256);
            byte[] data = new byte[] { 1, 2, 3 };

            using ECDsa key = ECDsa.Create(CosePublicKeyReader.ReadEs256(authenticator.CosePublicKey));

            Assert.IsTrue(key.VerifyData(data, authenticator.Sign(data), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
        }

        [TestMethod]
        public void Sign_Rs256_VerifiesUnderTheCosePublicKeyWithPkcs1Padding()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(CoseAlgorithm.Rs256);
            byte[] data = new byte[] { 1, 2, 3 };

            using RSA key = RSA.Create(CosePublicKeyReader.ReadRs256(authenticator.CosePublicKey));

            Assert.IsTrue(key.VerifyData(data, authenticator.Sign(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
    }
}
