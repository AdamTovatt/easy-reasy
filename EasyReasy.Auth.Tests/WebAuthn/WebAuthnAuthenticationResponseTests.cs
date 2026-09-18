namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The JSON contract of the body a page posts back after <c>navigator.credentials.get()</c>, and what
    /// the type refuses to be constructed with.
    /// </summary>
    /// <remarks>
    /// Everything here is a malformed request rather than a declined ceremony: a body that is not an
    /// authentication response never reaches a verification check, so it is an
    /// <see cref="ArgumentException"/> and not a
    /// <see cref="WebAuthnAuthenticationResult"/>.
    /// </remarks>
    [TestClass]
    public class WebAuthnAuthenticationResponseTests
    {
        [TestMethod]
        public void FromJson_BrowserResponse_ReadsEveryField()
        {
            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(WebAuthnAssertionTestData.BrowserJson());

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, response.Id);
            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, response.RawId);
            Assert.AreEqual(PublicKeyCredentialType.PublicKey, response.Type);
            Assert.AreEqual("platform", response.AuthenticatorAttachment);
            Assert.AreEqual(WebAuthnAssertionTestData.ClientDataJson, response.Response.ClientDataJson);
            Assert.AreEqual(WebAuthnAssertionTestData.AuthenticatorData, response.Response.AuthenticatorData);
            Assert.AreEqual(WebAuthnAssertionTestData.Signature, response.Response.Signature);
            Assert.AreEqual(WebAuthnAssertionTestData.UserHandle, response.Response.UserHandle);
        }

        [TestMethod]
        public void FromJson_ResponseWithoutOptionalFields_ReadsThemAsNull()
        {
            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(WebAuthnAssertionTestData.MinimalBrowserJson());

            Assert.IsNull(response.AuthenticatorAttachment);
            Assert.IsNull(response.Response.UserHandle);
        }

        [TestMethod]
        public void FromJson_UserHandleExplicitlyNull_ReadsAsAbsent()
        {
            // A browser writes no userHandle when the authenticator sent none, but a client library
            // serializing its own object may write a null instead. The two mean the same thing here.
            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(
                WebAuthnAssertionTestData.BrowserJson(userHandle: "null", quoteUserHandle: false));

            Assert.IsNull(response.Response.UserHandle);
        }

        [TestMethod]
        public void ToJson_ResponseWithoutOptionalFields_OmitsThem()
        {
            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(WebAuthnAssertionTestData.MinimalBrowserJson());

            string json = response.ToJson();

            StringAssert.Contains(json, WebAuthnAssertionResponse.SignatureFieldName);
            Assert.IsFalse(json.Contains(WebAuthnAssertionResponse.UserHandleFieldName), json);
            Assert.IsFalse(json.Contains(WebAuthnAuthenticationResponse.AuthenticatorAttachmentFieldName), json);
        }

        [TestMethod]
        public void ToJson_Response_RoundTripsThroughFromJson()
        {
            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(WebAuthnAssertionTestData.BrowserJson());

            WebAuthnAuthenticationResponse roundTripped = WebAuthnAuthenticationResponse.FromJson(response.ToJson());

            Assert.AreEqual(response.Id, roundTripped.Id);
            Assert.AreEqual(response.Response.Signature, roundTripped.Response.Signature);
            Assert.AreEqual(response.Response.UserHandle, roundTripped.Response.UserHandle);
        }

        [TestMethod]
        public void ToString_Response_IsItsJson()
        {
            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(WebAuthnAssertionTestData.BrowserJson());

            Assert.AreEqual(response.ToJson(), response.ToString());
        }

        [DataTestMethod]
        [DataRow(WebAuthnAuthenticationResponse.IdFieldName)]
        [DataRow(WebAuthnAuthenticationResponse.RawIdFieldName)]
        [DataRow(WebAuthnAuthenticationResponse.TypeFieldName)]
        [DataRow(WebAuthnAuthenticationResponse.ResponseFieldName)]
        public void FromJson_ResponseMissingARequiredField_Throws(string missingField)
        {
            string json = WebAuthnAssertionTestData.BrowserJson().Replace($"\"{missingField}\"", "\"notTheField\"");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnAuthenticationResponse.FromJson(json));
        }

        [DataTestMethod]
        [DataRow(WebAuthnAssertionResponse.ClientDataJsonFieldName)]
        [DataRow(WebAuthnAssertionResponse.AuthenticatorDataFieldName)]
        [DataRow(WebAuthnAssertionResponse.SignatureFieldName)]
        public void FromJson_AssertionMissingARequiredField_Throws(string missingField)
        {
            string json = WebAuthnAssertionTestData.BrowserJson().Replace($"\"{missingField}\"", "\"notTheField\"");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnAuthenticationResponse.FromJson(json));
        }

        [TestMethod]
        public void FromJson_TextThatIsNotJson_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => WebAuthnAuthenticationResponse.FromJson("not json"));
        }

        [TestMethod]
        public void FromJson_JsonThatIsNotAnObject_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => WebAuthnAuthenticationResponse.FromJson("[]"));
        }

        [TestMethod]
        public void FromJson_NullJson_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => WebAuthnAuthenticationResponse.FromJson(null!));
        }

        [TestMethod]
        public void Constructor_IdAndRawIdStandingForDifferentBytes_Throws()
        {
            // The browser writes the same credential id twice. A response where they differ leaves a choice
            // of which one the credential is, and the lookup and the signature check would answer it
            // differently.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnAuthenticationResponse(
                    WebAuthnResponseTestData.CredentialId,
                    WebAuthnResponseTestData.OtherCredentialId,
                    PublicKeyCredentialType.PublicKey,
                    WebAuthnAssertionTestData.NewAssertion()));

            Assert.AreEqual("rawId", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_PaddedRawId_IsAcceptedAsTheSameCredential()
        {
            // Compared as bytes rather than as text, so a client library that pads one of the two spellings
            // has not reported two different credentials.
            WebAuthnAuthenticationResponse response = new WebAuthnAuthenticationResponse(
                WebAuthnResponseTestData.CredentialId,
                WebAuthnResponseTestData.CredentialId + "=",
                PublicKeyCredentialType.PublicKey,
                WebAuthnAssertionTestData.NewAssertion());

            // The only case where the two spellings differ, so it is the only one that can catch either
            // property being filled from the other argument. Both are asserted for that reason.
            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, response.Id);
            Assert.AreEqual(WebAuthnResponseTestData.CredentialId + "=", response.RawId);
        }

        [TestMethod]
        public void Constructor_TypeThatIsNotPublicKey_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnAuthenticationResponse(
                    WebAuthnResponseTestData.CredentialId,
                    WebAuthnResponseTestData.CredentialId,
                    "not-public-key",
                    WebAuthnAssertionTestData.NewAssertion()));

            Assert.AreEqual("type", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("id")]
        [DataRow("rawId")]
        [DataRow("type")]
        [DataRow("response")]
        public void Constructor_NullArgument_NamesIt(string nullArgumentName)
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => new WebAuthnAuthenticationResponse(
                    nullArgumentName == "id" ? null! : WebAuthnResponseTestData.CredentialId,
                    nullArgumentName == "rawId" ? null! : WebAuthnResponseTestData.CredentialId,
                    nullArgumentName == "type" ? null! : PublicKeyCredentialType.PublicKey,
                    nullArgumentName == "response" ? null! : WebAuthnAssertionTestData.NewAssertion()));

            Assert.AreEqual(nullArgumentName, exception.ParamName);
        }
    }
}
