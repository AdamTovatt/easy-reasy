namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The JSON contract of the body a page posts back after <c>navigator.credentials.get()</c>, and what
    /// the type refuses to be constructed with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here is a malformed request rather than a declined ceremony: a body that is not an
    /// authentication response never reaches a verification check, so it is an
    /// <see cref="ArgumentException"/> and not a
    /// <see cref="WebAuthnAuthenticationResult"/>.
    /// </para>
    /// <para>
    /// Run under the hostile naming policy, like the registration counterpart. Under the library's own
    /// camelCase options every name on this path but <c>clientDataJSON</c> comes out right whether or not
    /// it is pinned, so the <c>ToJson</c> assertions below would pass against a type whose
    /// <c>JsonPropertyName</c> attributes had all been deleted.
    /// </para>
    /// </remarks>
    [TestClass]
    [DoNotParallelize]
    public class WebAuthnAuthenticationResponseTests : HostileNamingPolicyTestBase
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
        public void FromJson_IdAndRawIdSpelledDifferently_KeepsEachInItsOwnField()
        {
            // Two spellings of the same bytes, which is the only way the fields can differ and still be
            // accepted — and so the only case that can catch one being read into the other.
            string json = WebAuthnAssertionTestData.BrowserJson().Replace(
                $"\"{WebAuthnAuthenticationResponse.RawIdFieldName}\":\"{WebAuthnResponseTestData.CredentialId}\"",
                $"\"{WebAuthnAuthenticationResponse.RawIdFieldName}\":\"{WebAuthnResponseTestData.CredentialId}=\"");

            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(json);

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, response.Id);
            Assert.AreEqual(WebAuthnResponseTestData.CredentialId + "=", response.RawId);
        }

        [TestMethod]
        public void FromJson_UnknownFields_AreIgnored()
        {
            // The browser writes clientExtensionResults, which this library does not read, and the set of
            // fields grows with the spec — so an unknown one must not be an error.
            string json = WebAuthnAssertionTestData.BrowserJson().Replace(
                "\"clientExtensionResults\":{}",
                "\"clientExtensionResults\":{\"credProps\":{\"rk\":true}},\"somethingAddedLater\":[1,2,3]");

            WebAuthnAuthenticationResponse response = WebAuthnAuthenticationResponse.FromJson(json);

            Assert.AreEqual(WebAuthnResponseTestData.CredentialId, response.Id);
        }

        [DataTestMethod]
        [DataRow(WebAuthnAuthenticationResponse.IdFieldName)]
        [DataRow(WebAuthnAuthenticationResponse.RawIdFieldName)]
        [DataRow(WebAuthnAuthenticationResponse.TypeFieldName)]
        public void FromJson_RequiredFieldPresentButEmpty_SaysItIsEmpty(string emptyField)
        {
            string json = WebAuthnAssertionTestData.BrowserJson().Replace(
                $"\"{emptyField}\":\"{ValueOf(emptyField)}\"",
                $"\"{emptyField}\":\"\"");

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => WebAuthnAuthenticationResponse.FromJson(json));

            StringAssert.Contains(exception.Message, $"has an empty \"{emptyField}\"");
        }

        [DataTestMethod]
        [DataRow(WebAuthnAuthenticationResponse.IdFieldName)]
        [DataRow(WebAuthnAuthenticationResponse.RawIdFieldName)]
        [DataRow(WebAuthnAuthenticationResponse.TypeFieldName)]
        public void FromJson_RequiredStringThatIsNotAString_Throws(string field)
        {
            string json = WebAuthnAssertionTestData.BrowserJson().Replace(
                $"\"{field}\":\"{ValueOf(field)}\"",
                $"\"{field}\":42");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnAuthenticationResponse.FromJson(json));
        }

        [TestMethod]
        public void FromJson_OptionalStringThatIsNotAString_Throws()
        {
            string json = WebAuthnAssertionTestData.BrowserJson().Replace(
                $"\"{WebAuthnAuthenticationResponse.AuthenticatorAttachmentFieldName}\":\"platform\"",
                $"\"{WebAuthnAuthenticationResponse.AuthenticatorAttachmentFieldName}\":42");

            Assert.ThrowsException<ArgumentException>(() => WebAuthnAuthenticationResponse.FromJson(json));
        }

        [DataTestMethod]
        [DataRow("id")]
        [DataRow("rawId")]
        public void Constructor_IdThatIsNotBase64Url_NamesIt(string badField)
        {
            // The guard lives in the shared envelope, but a constructor that stopped calling it would leave
            // every test that goes through the shared reader green — so each path pins its own call.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewResponse(badField, "not base64url!"));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("id")]
        [DataRow("rawId")]
        public void Constructor_IdThatStandsForNoBytes_NamesIt(string badField)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewResponse(badField, string.Empty));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("id")]
        [DataRow("rawId")]
        public void Constructor_IdLongerThanAnyCredentialField_NamesIt(string badField)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewResponse(badField, new string('A', WebAuthnResponseReader.MaximumEncodedFieldLength + 4)));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [TestMethod]
        public void Constructor_OverlongCredentialType_DoesNotQuoteItBack()
        {
            // The type is the one envelope field repeated verbatim into the message, and it arrives from
            // the network. An application is told to log these, so a value long enough to bury a log entry
            // is described rather than quoted.
            string overlongType = new string('t', WebAuthnCredentialResponse.MaximumQuotedTypeLength + 1);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnAuthenticationResponse(
                    WebAuthnResponseTestData.CredentialId,
                    WebAuthnResponseTestData.CredentialId,
                    overlongType,
                    WebAuthnAssertionTestData.NewAssertion()));

            Assert.IsFalse(exception.Message.Contains(overlongType), exception.Message);
            StringAssert.Contains(exception.Message, $"{overlongType.Length} characters");
        }

        [TestMethod]
        public void Constructor_CredentialTypeCarryingANewline_DoesNotQuoteItBack()
        {
            // Short enough to quote, and carrying what it would take to forge a second log line.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnAuthenticationResponse(
                    WebAuthnResponseTestData.CredentialId,
                    WebAuthnResponseTestData.CredentialId,
                    "public-key\n[warn] credential accepted",
                    WebAuthnAssertionTestData.NewAssertion()));

            Assert.IsFalse(exception.Message.Contains('\n'), exception.Message);
            StringAssert.Contains(exception.Message, "control character");
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

        /// <summary>The value the browser JSON carries for one of the envelope's required string fields.</summary>
        private static string ValueOf(string field)
        {
            return field == WebAuthnAuthenticationResponse.TypeFieldName
                ? PublicKeyCredentialType.PublicKey
                : WebAuthnResponseTestData.CredentialId;
        }

        /// <summary>A response with one of the two credential ids replaced.</summary>
        private static WebAuthnAuthenticationResponse NewResponse(string fieldName, string value)
        {
            return new WebAuthnAuthenticationResponse(
                fieldName == "id" ? value : WebAuthnResponseTestData.CredentialId,
                fieldName == "rawId" ? value : WebAuthnResponseTestData.CredentialId,
                PublicKeyCredentialType.PublicKey,
                WebAuthnAssertionTestData.NewAssertion());
        }
    }
}
