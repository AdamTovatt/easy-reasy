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
            string tooLong = new string('A', WebAuthnResponseReader.MaximumEncodedFieldLength + 4);

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
            string atLimit = new string('A', WebAuthnResponseReader.MaximumEncodedFieldLength);

            WebAuthnAttestationResponse response = NewResponse(longField, atLimit);

            // Asserts the value survived rather than only that nothing threw: the return is non-nullable,
            // so a null check here could never fail whatever the constructor did with it.
            string kept = longField == "clientDataJson" ? response.ClientDataJson : response.AttestationObject;
            Assert.AreEqual(atLimit, kept);
        }

        [TestMethod]
        public void Constructor_MoreTransportsThanAnyAuthenticatorReports_Throws()
        {
            // The one value in a registered credential with no length of its own, and the one the README
            // tells an application to persist — so it is bounded before it becomes a database row.
            string[] tooMany = Enumerable.Repeat("usb", WebAuthnAttestationResponse.MaximumTransportCount + 1).ToArray();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnAttestationResponse(WebAuthnResponseTestData.ClientDataJson, WebAuthnResponseTestData.AttestationObject, tooMany));

            Assert.AreEqual("transports", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_TransportLongerThanAnySpecifiedName_Throws()
        {
            string[] oneLongOne = new[] { "usb", new string('t', WebAuthnAttestationResponse.MaximumTransportLength + 1) };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new WebAuthnAttestationResponse(WebAuthnResponseTestData.ClientDataJson, WebAuthnResponseTestData.AttestationObject, oneLongOne));

            Assert.AreEqual("transports", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_TransportsExactlyAtTheLimits_AreAccepted()
        {
            // Both bounds from the accepting side, without which the two tests above would pass against
            // limits of zero.
            string[] atTheLimits = Enumerable.Repeat(new string('t', WebAuthnAttestationResponse.MaximumTransportLength), WebAuthnAttestationResponse.MaximumTransportCount).ToArray();

            WebAuthnAttestationResponse response = new WebAuthnAttestationResponse(
                WebAuthnResponseTestData.ClientDataJson,
                WebAuthnResponseTestData.AttestationObject,
                atTheLimits);

            Assert.AreEqual(WebAuthnAttestationResponse.MaximumTransportCount, response.Transports!.Count);
        }

        [TestMethod]
        public void Constructor_UnrecognisedTransport_IsKept()
        {
            // The vocabulary grows with the spec, so an unknown name is not an error — bounding the length
            // and the count is a different rule from knowing the value.
            WebAuthnAttestationResponse response = new WebAuthnAttestationResponse(
                WebAuthnResponseTestData.ClientDataJson,
                WebAuthnResponseTestData.AttestationObject,
                new[] { "something-added-later" });

            Assert.AreEqual("something-added-later", response.Transports!.Single());
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
