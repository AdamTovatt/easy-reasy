namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the attestation response refuses to be constructed with. Its two encoded fields are the only
    /// ones verification reads, so anything that is not base64url of something is rejected here rather than
    /// reaching a parser that would describe it less clearly.
    /// </summary>
    [TestClass]
    public class WebAuthnAttestationResponseTests
    {
        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("attestationObject")]
        public void Constructor_FieldThatIsNotBase64Url_NamesIt(string badField)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewResponse(badField, "not base64url!"));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("attestationObject")]
        public void Constructor_FieldThatStandsForNoBytes_NamesIt(string badField)
        {
            // Whitespace is valid base64url and decodes to nothing, so only an explicit guard rejects it.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewResponse(badField, "   "));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("attestationObject")]
        public void Constructor_FieldLongerThanAnyCredentialCarries_NamesIt(string badField)
        {
            // Untrusted and arriving before anything has decided this is a credential, so it is bounded
            // before it is decoded rather than after. Four characters over rather than one, because a
            // length base64url cannot have would be rejected as malformed and this would pass with no
            // bound in place at all.
            string tooLong = new string('A', WebAuthnResponseField.MaximumEncodedFieldLength + 4);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewResponse(badField, tooLong));

            Assert.AreEqual(badField, exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("attestationObject")]
        public void Constructor_FieldAtTheLengthLimit_IsAccepted(string longField)
        {
            // Pins which side of the bound is rejected. The limit is a multiple of four, so a field of
            // exactly that length is valid base64url and nothing but the bound could refuse it.
            string atLimit = new string('A', WebAuthnResponseField.MaximumEncodedFieldLength);

            Assert.IsNotNull(NewResponse(longField, atLimit));
        }

        [DataTestMethod]
        [DataRow("clientDataJson")]
        [DataRow("attestationObject")]
        public void Constructor_NullArgument_NamesIt(string nullArgumentName)
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => NewResponse(nullArgumentName, null!));

            Assert.AreEqual(nullArgumentName, exception.ParamName);
        }

        [TestMethod]
        public void Constructor_DistinctFields_KeepsThemApart()
        {
            // Two different values, so a constructor that assigned one field from the other fails here.
            WebAuthnAttestationResponse response = new WebAuthnAttestationResponse(
                WebAuthnResponseTestData.ClientDataJson,
                WebAuthnResponseTestData.AttestationObject);

            Assert.AreEqual(WebAuthnResponseTestData.ClientDataJson, response.ClientDataJson);
            Assert.AreEqual(WebAuthnResponseTestData.AttestationObject, response.AttestationObject);
        }

        [TestMethod]
        public void Constructor_NoTransports_LeavesThemNull()
        {
            WebAuthnAttestationResponse response = new WebAuthnAttestationResponse(
                WebAuthnResponseTestData.ClientDataJson,
                WebAuthnResponseTestData.AttestationObject);

            Assert.IsNull(response.Transports);
        }

        /// <summary>
        /// Builds a response whose named field carries <paramref name="value"/> and whose other field is
        /// valid, so the assertion pins which of the two was rejected.
        /// </summary>
        private static WebAuthnAttestationResponse NewResponse(string fieldName, string value)
        {
            return new WebAuthnAttestationResponse(
                fieldName == "clientDataJson" ? value : WebAuthnResponseTestData.ClientDataJson,
                fieldName == "attestationObject" ? value : WebAuthnResponseTestData.AttestationObject);
        }
    }
}
