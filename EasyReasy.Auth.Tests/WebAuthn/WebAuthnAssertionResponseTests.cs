namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the assertion inside an authentication response refuses to be constructed with. Its three
    /// required fields carry the bytes that were signed, and the optional one is handed to applications as
    /// base64url they may decode — so all four are held to the same rules.
    /// </summary>
    [TestClass]
    public class WebAuthnAssertionResponseTests
    {
        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("authenticatorData")]
        [DataRow("signature")]
        [DataRow("userHandle")]
        public void Constructor_FieldThatIsNotBase64Url_NamesIt(string badField)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAssertion(badField, "not base64url!"));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("authenticatorData")]
        [DataRow("signature")]
        [DataRow("userHandle")]
        public void Constructor_FieldStandingForNoBytes_NamesIt(string badField)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAssertion(badField, string.Empty));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("authenticatorData")]
        [DataRow("signature")]
        [DataRow("userHandle")]
        public void Constructor_FieldLongerThanAnyCredentialField_NamesIt(string badField)
        {
            // One character past the bound rather than an arbitrarily huge value: a test that overshot
            // would pass just as well against a bound set anywhere below it.
            string tooLong = new string('A', WebAuthnResponseReader.MaximumEncodedFieldLength + 4);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAssertion(badField, tooLong));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("authenticatorData")]
        [DataRow("signature")]
        [DataRow("userHandle")]
        public void Constructor_FieldExactlyAtTheLimit_IsAccepted(string field)
        {
            // The other half of the bound: without this the test above would pass against a bound of zero.
            string atTheLimit = new string('A', WebAuthnResponseReader.MaximumEncodedFieldLength);

            WebAuthnAssertionResponse assertion = NewAssertion(field, atTheLimit);

            // Asserts the value survived rather than only that nothing threw: the return is non-nullable,
            // so a null check here could never fail whatever the constructor did with it.
            string? kept = field switch
            {
                "clientDataJson" => assertion.ClientDataJson,
                "authenticatorData" => assertion.AuthenticatorData,
                "signature" => assertion.Signature,
                _ => assertion.UserHandle,
            };

            Assert.AreEqual(atTheLimit, kept);
        }

        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("authenticatorData")]
        [DataRow("signature")]
        public void Constructor_NullRequiredField_NamesIt(string nullArgumentName)
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => NewAssertion(nullArgumentName, null!));

            Assert.AreEqual(nullArgumentName, exception.ParamName);
        }

        [TestMethod]
        public void Constructor_NoUserHandle_IsAccepted()
        {
            // An authenticator answering a non-discoverable ceremony need not send one, which is the
            // ordinary case for a second factor — requiring it would reject every such assertion.
            WebAuthnAssertionResponse assertion = new WebAuthnAssertionResponse(
                WebAuthnAssertionTestData.ClientDataJson,
                WebAuthnAssertionTestData.AuthenticatorData,
                WebAuthnAssertionTestData.Signature);

            Assert.IsNull(assertion.UserHandle);
        }

        [TestMethod]
        public void Constructor_EveryField_KeepsEachOneSeparately()
        {
            // Four distinct values, none a prefix of another, so a constructor assigning any of them from
            // the wrong argument fails here.
            WebAuthnAssertionResponse assertion = WebAuthnAssertionTestData.NewAssertion();

            Assert.AreEqual(WebAuthnAssertionTestData.ClientDataJson, assertion.ClientDataJson);
            Assert.AreEqual(WebAuthnAssertionTestData.AuthenticatorData, assertion.AuthenticatorData);
            Assert.AreEqual(WebAuthnAssertionTestData.Signature, assertion.Signature);
            Assert.AreEqual(WebAuthnAssertionTestData.UserHandle, assertion.UserHandle);
        }

        private static WebAuthnAssertionResponse NewAssertion(string fieldName, string value)
        {
            return new WebAuthnAssertionResponse(
                fieldName == "clientDataJson" ? value : WebAuthnAssertionTestData.ClientDataJson,
                fieldName == "authenticatorData" ? value : WebAuthnAssertionTestData.AuthenticatorData,
                fieldName == "signature" ? value : WebAuthnAssertionTestData.Signature,
                fieldName == "userHandle" ? value : WebAuthnAssertionTestData.UserHandle);
        }
    }
}
