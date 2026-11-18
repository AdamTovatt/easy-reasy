using System.Buffers.Text;
using System.Text.Json.Nodes;

namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// The authenticator refuses where a real authenticator or browser refuses, and holds exactly one
    /// credential.
    /// </summary>
    [TestClass]
    public class WebAuthnTestAuthenticatorRefusalTests
    {
        /// <summary>A subdomain of <see cref="TestRelyingParty.Id"/>, as a relying-party id of its own.</summary>
        private const string SubdomainId = "login.example.com";

        /// <summary>The origin served at <see cref="SubdomainId"/>.</summary>
        private const string SubdomainOrigin = "https://login.example.com";

        [TestMethod]
        public void Register_ExcludeCredentialsNamingItsCredential_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator().CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred,
                new[] { Base64Url.EncodeToString(authenticator.CredentialId) });

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => authenticator.Register(options, TestRelyingParty.Origin));

            StringAssert.Contains(exception.Message, "excludeCredentials");
        }

        [TestMethod]
        public void Register_ExcludeCredentialsNamingOnlyOtherCredentials_Registers()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            using WebAuthnTestAuthenticator other = WebAuthnTestAuthenticator.Create();
            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator().CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred,
                new[] { Base64Url.EncodeToString(other.CredentialId) });

            WebAuthnTestRegistration registration = authenticator.Register(options, TestRelyingParty.Origin);

            Assert.IsTrue(TestRelyingParty.NewVerifier().VerifyRegistration(registration.Response, options.Challenge, WebAuthnUserVerificationRequirement.Preferred).Success);
        }

        [TestMethod]
        public void Register_PubKeyCredParamsNotOfferingItsAlgorithm_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(CoseAlgorithm.Rs256);
            JsonObject options = CreationOptionsJson();
            options["pubKeyCredParams"] = new JsonArray(new JsonObject { ["type"] = "public-key", ["alg"] = (int)CoseAlgorithm.Es256 });

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => authenticator.Register(options.ToJsonString(), TestRelyingParty.Origin));

            StringAssert.Contains(exception.Message, "pubKeyCredParams");
        }

        [TestMethod]
        public void Register_AfterAPubKeyCredParamsRefusal_StillRegisters()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(CoseAlgorithm.Rs256);
            JsonObject refused = CreationOptionsJson();
            refused["pubKeyCredParams"] = new JsonArray();
            Assert.ThrowsException<InvalidOperationException>(() => authenticator.Register(refused.ToJsonString(), TestRelyingParty.Origin));

            WebAuthnRegistrationResult result = RegisterWithFreshOptions(authenticator);

            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
        }

        [TestMethod]
        public void Register_AfterAnExcludeCredentialsRefusal_StillRegisters()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            PublicKeyCredentialCreationOptions refused = TestRelyingParty.NewGenerator().CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred,
                new[] { Base64Url.EncodeToString(authenticator.CredentialId) });
            Assert.ThrowsException<InvalidOperationException>(() => authenticator.Register(refused, TestRelyingParty.Origin));

            WebAuthnRegistrationResult result = RegisterWithFreshOptions(authenticator);

            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
        }

        [TestMethod]
        public void Register_Refused_RecordsNoUserHandle()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(CoseAlgorithm.Rs256);
            JsonObject refused = CreationOptionsJson();
            refused["pubKeyCredParams"] = new JsonArray();

            Assert.ThrowsException<InvalidOperationException>(() => authenticator.Register(refused.ToJsonString(), TestRelyingParty.Origin));

            Assert.IsNull(authenticator.UserHandle);
        }

        [TestMethod]
        public void Register_RpIdNeitherTheOriginsHostNorAParentOfIt_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator("other.example").CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => authenticator.Register(options, TestRelyingParty.Origin));

            StringAssert.Contains(exception.Message, "parent domain");
        }

        [TestMethod]
        public void Register_RpIdThatIsAParentDomainOfTheOrigin_Registers()
        {
            const string subdomainOrigin = "https://login.example.com";
            WebAuthnRelyingParty relyingParty = new WebAuthnRelyingParty(TestRelyingParty.Id, "Contoso", new[] { subdomainOrigin });
            PublicKeyCredentialCreationOptions options = new WebAuthnOptionsGenerator(relyingParty).CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred);
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            WebAuthnTestRegistration registration = authenticator.Register(options, subdomainOrigin);
            WebAuthnRegistrationResult result = new WebAuthnVerifier(relyingParty).VerifyRegistration(
                registration.Response,
                options.Challenge,
                WebAuthnUserVerificationRequirement.Preferred);

            Assert.IsTrue(result.Success, $"{result.FailureReason}: {result.FailureMessage}");
        }

        [TestMethod]
        public void Assert_AllowCredentialsNotNamingItsCredential_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            using WebAuthnTestAuthenticator other = WebAuthnTestAuthenticator.Create();
            TestRelyingParty.RegisterSecondFactor(authenticator);
            PublicKeyCredentialRequestOptions options = TestRelyingParty.NewGenerator().CreateAuthenticationOptions(
                new[] { Base64Url.EncodeToString(other.CredentialId) },
                WebAuthnUserVerificationRequirement.Preferred);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => authenticator.Assert(options, TestRelyingParty.Origin));

            StringAssert.Contains(exception.Message, "allowCredentials");
        }

        [TestMethod]
        public void Assert_RpIdOtherThanTheRegisteredOne_Throws()
        {
            // A subdomain's own relying party, at its own origin: the origin check passes, so what is refused
            // is that the credential was registered under example.com.
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            TestRelyingParty.RegisterPasskey(authenticator);
            PublicKeyCredentialRequestOptions options = TestRelyingParty.NewGenerator(SubdomainId).CreateDiscoverableAuthenticationOptions();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => authenticator.Assert(options, SubdomainOrigin));

            StringAssert.Contains(exception.Message, "registered with");
        }

        [TestMethod]
        public void Assert_RpIdNeitherTheOriginsHostNorAParentOfIt_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            TestRelyingParty.RegisterPasskey(authenticator);
            PublicKeyCredentialRequestOptions options = TestRelyingParty.NewGenerator().CreateDiscoverableAuthenticationOptions();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => authenticator.Assert(options, "https://other.example"));

            StringAssert.Contains(exception.Message, "parent domain");
        }

        [TestMethod]
        public void Assert_RefusedWithACounter_LeavesTheCounterAlone()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.SignCount = 3;
            TestRelyingParty.RegisterPasskey(authenticator);
            PublicKeyCredentialRequestOptions options = TestRelyingParty.NewGenerator(SubdomainId).CreateDiscoverableAuthenticationOptions();

            Assert.ThrowsException<InvalidOperationException>(() => authenticator.Assert(options, SubdomainOrigin));

            Assert.AreEqual(3u, authenticator.SignCount);
        }

        [TestMethod]
        public void Register_Twice_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            TestRelyingParty.RegisterSecondFactor(authenticator);

            Assert.ThrowsException<InvalidOperationException>(() => TestRelyingParty.RegisterSecondFactor(authenticator));
        }

        [TestMethod]
        public void Assert_BeforeRegister_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            Assert.ThrowsException<InvalidOperationException>(
                () => authenticator.Assert(TestRelyingParty.NewGenerator().CreateDiscoverableAuthenticationOptions(), TestRelyingParty.Origin));
        }

        [TestMethod]
        public void Create_EmptyCredentialId_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => WebAuthnTestAuthenticator.Create(credentialId: Array.Empty<byte>()));
        }

        [TestMethod]
        public void Create_CredentialIdOneByteLongerThanWebAuthnAllows_Throws()
        {
            byte[] credentialId = new byte[WebAuthnTestAuthenticator.MaximumCredentialIdLength + 1];

            Assert.ThrowsException<ArgumentException>(() => WebAuthnTestAuthenticator.Create(credentialId: credentialId));
        }

        [TestMethod]
        public void Create_AlgorithmItDoesNotSignWith_Throws()
        {
            const CoseAlgorithm edDsa = (CoseAlgorithm)(-8);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => WebAuthnTestAuthenticator.Create(edDsa));
        }

        private static WebAuthnRegistrationResult RegisterWithFreshOptions(WebAuthnTestAuthenticator authenticator)
        {
            PublicKeyCredentialCreationOptions options = TestRelyingParty.NewGenerator().CreateRegistrationOptions(
                TestRelyingParty.NewUser(),
                WebAuthnUserVerificationRequirement.Preferred);
            WebAuthnTestRegistration registration = authenticator.Register(options, TestRelyingParty.Origin);

            return TestRelyingParty.NewVerifier().VerifyRegistration(registration.Response, options.Challenge, WebAuthnUserVerificationRequirement.Preferred);
        }

        private static JsonObject CreationOptionsJson()
        {
            string json = TestRelyingParty.NewGenerator()
                .CreateRegistrationOptions(TestRelyingParty.NewUser(), WebAuthnUserVerificationRequirement.Preferred)
                .ToJson();

            return JsonNode.Parse(json)?.AsObject() ?? throw new AssertFailedException("The options are not a JSON object.");
        }
    }
}
