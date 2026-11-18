using System.Buffers.Text;

namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// Options JSON or an origin that no browser would accept is an <see cref="ArgumentException"/> naming
    /// the problem, and <c>pubKeyCredParams</c> is read the way a browser reads it.
    /// </summary>
    [TestClass]
    public class WebAuthnTestAuthenticatorOptionsInputTests
    {
        private const string ValidCreationOptions =
            "{\"rp\":{\"id\":\"example.com\"},\"user\":{\"id\":\"AQ\"},\"challenge\":\"AQ\",\"pubKeyCredParams\":[{\"type\":\"public-key\",\"alg\":-7}]}";

        [DataTestMethod]
        [DataRow("not json", DisplayName = "not JSON")]
        [DataRow("[]", DisplayName = "root not an object")]
        [DataRow("{\"user\":{\"id\":\"AQ\"},\"challenge\":\"AQ\",\"pubKeyCredParams\":[{\"type\":\"public-key\",\"alg\":-7}]}", DisplayName = "missing rp")]
        [DataRow("{\"rp\":{\"id\":\"example.com\"},\"challenge\":\"AQ\",\"pubKeyCredParams\":[{\"type\":\"public-key\",\"alg\":-7}]}", DisplayName = "missing user")]
        [DataRow("{\"rp\":{\"id\":\"example.com\"},\"user\":{\"id\":\"AQ\"},\"pubKeyCredParams\":[{\"type\":\"public-key\",\"alg\":-7}]}", DisplayName = "missing challenge")]
        [DataRow("{\"rp\":{\"id\":\"example.com\"},\"user\":{\"id\":\"AQ\"},\"challenge\":\"not base64url!\",\"pubKeyCredParams\":[{\"type\":\"public-key\",\"alg\":-7}]}", DisplayName = "challenge not base64url")]
        [DataRow("{\"rp\":{\"id\":\"example.com\"},\"user\":{\"id\":\"AQ\"},\"challenge\":\"AQ\"}", DisplayName = "missing pubKeyCredParams")]
        [DataRow("{\"rp\":{\"id\":\"example.com\"},\"user\":{\"id\":\"AQ\"},\"challenge\":\"AQ\",\"pubKeyCredParams\":[{\"type\":\"public-key\",\"alg\":\"ES256\"}]}", DisplayName = "alg not an integer")]
        [DataRow("{\"rp\":{\"id\":\"example.com\"},\"user\":{\"id\":\"AQ\"},\"challenge\":\"AQ\",\"pubKeyCredParams\":[{\"type\":\"public-key\",\"alg\":-7}],\"excludeCredentials\":{}}", DisplayName = "excludeCredentials not an array")]
        public void Register_MalformedOptionsJson_ThrowsArgumentException(string optionsJson)
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            Assert.ThrowsException<ArgumentException>(() => authenticator.Register(optionsJson, TestRelyingParty.Origin));
        }

        [DataTestMethod]
        [DataRow("[]", DisplayName = "root not an object")]
        [DataRow("{\"rpId\":\"example.com\"}", DisplayName = "missing challenge")]
        [DataRow("{\"challenge\":\"AQ\",\"rpId\":\"example.com\",\"allowCredentials\":{}}", DisplayName = "allowCredentials not an array")]
        [DataRow("{\"challenge\":\"AQ\",\"rpId\":\"example.com\",\"allowCredentials\":[{\"type\":\"public-key\"}]}", DisplayName = "allowCredentials entry without an id")]
        public void Assert_MalformedOptionsJson_ThrowsArgumentException(string optionsJson)
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            TestRelyingParty.RegisterPasskey(authenticator);

            Assert.ThrowsException<ArgumentException>(() => authenticator.Assert(optionsJson, TestRelyingParty.Origin));
        }

        [DataTestMethod]
        [DataRow("example.com", DisplayName = "no scheme")]
        [DataRow("/example", DisplayName = "a path, which is a file URL on Unix")]
        [DataRow("ftp://example.com", DisplayName = "not http or https")]
        [DataRow("https://example.com/app", DisplayName = "with a path")]
        [DataRow("https://example.com/?next=1", DisplayName = "with a query")]
        [DataRow("https://user@example.com", DisplayName = "with user info")]
        public void Register_OriginThatIsNotAnHttpOrigin_ThrowsArgumentException(string origin)
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            Assert.ThrowsException<ArgumentException>(() => authenticator.Register(ValidCreationOptions, origin));
        }

        [TestMethod]
        public void Assert_OriginThatIsNotAnHttpOrigin_ThrowsArgumentException()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            TestRelyingParty.RegisterPasskey(authenticator);

            Assert.ThrowsException<ArgumentException>(
                () => authenticator.Assert(TestRelyingParty.NewGenerator().CreateDiscoverableAuthenticationOptions(), "https://example.com/app"));
        }

        [TestMethod]
        public void Register_ItsAlgorithmOfferedOnlyUnderAnotherCredentialType_Throws()
        {
            // A browser skips pubKeyCredParams entries of a type it does not know, so ES256 offered under
            // another type is not offered at all.
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            string options = ValidCreationOptions.Replace("\"type\":\"public-key\"", "\"type\":\"future-key\"");

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => authenticator.Register(options, TestRelyingParty.Origin));

            StringAssert.Contains(exception.Message, "pubKeyCredParams");
        }

        [TestMethod]
        public void Register_ItsAlgorithmOfferedBesideAnotherCredentialType_Registers()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            string options = ValidCreationOptions.Replace(
                "[{\"type\":\"public-key\",\"alg\":-7}]",
                "[{\"type\":\"future-key\",\"alg\":-8},{\"type\":\"public-key\",\"alg\":-7}]");

            WebAuthnTestRegistration registration = authenticator.Register(options, TestRelyingParty.Origin);

            Assert.AreEqual(Base64Url.EncodeToString(authenticator.CredentialId), registration.Response.Id);
        }
    }
}
