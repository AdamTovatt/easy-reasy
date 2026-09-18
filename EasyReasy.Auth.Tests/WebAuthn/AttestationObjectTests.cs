namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class AttestationObjectTests
    {
        [TestMethod]
        public void Parse_NoneFormat_ReturnsTheAuthenticatorDataItCarried()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            byte[] authenticatorData = BuildAuthenticatorData(authenticator);

            AttestationObject parsed = AttestationObject.Parse(
                CborTestEncoder.EncodeAttestationObject("none", authenticatorData, CborTestEncoder.EncodeEmptyMap()));

            CollectionAssert.AreEqual(WebAuthnTestData.RelyingPartyIdHash, parsed.AuthenticatorData.RelyingPartyIdHash);
            CollectionAssert.AreEqual(authenticator.CredentialId, parsed.AuthenticatorData.CredentialId);
        }

        [DataTestMethod]
        [DataRow("packed")]
        [DataRow("tpm")]
        [DataRow("apple")]
        [DataRow("android-key")]
        [DataRow("fido-u2f")]
        [DataRow("")]
        public void Parse_AnyFormatOtherThanNone_ThrowsUnsupportedAttestationFormat(string format)
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            byte[] encoded = CborTestEncoder.EncodeAttestationObject(format, BuildAuthenticatorData(authenticator), CborTestEncoder.EncodeEmptyMap());

            WebAuthnParseAssert.Throws(WebAuthnParseError.UnsupportedAttestationFormat, () => AttestationObject.Parse(encoded));
        }

        [TestMethod]
        public void Parse_NoneFormatCarryingAnAttestationStatement_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            byte[] statement = new CborMapBuilder()
                .With(CborTestEncoder.EncodeTextString("sig"), CborTestEncoder.EncodeByteString(new byte[] { 0x01 }))
                .Build();

            byte[] encoded = CborTestEncoder.EncodeAttestationObject("none", BuildAuthenticatorData(authenticator), statement);

            WebAuthnParseException exception = AssertMalformed(encoded);
            StringAssert.Contains(exception.Message, "empty map");
        }

        [TestMethod]
        public void Parse_AttestationStatementThatIsNotAMap_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            byte[] encoded = CborTestEncoder.EncodeAttestationObject(
                "none",
                BuildAuthenticatorData(authenticator),
                CborTestEncoder.EncodeTextString("not a map"));

            WebAuthnParseException exception = AssertMalformed(encoded);
            StringAssert.Contains(exception.Message, "empty map");
        }

        [TestMethod]
        public void Parse_WithNoAttestationStatementAtAll_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            byte[] encoded = CborTestEncoder.EncodeAttestationObject("none", BuildAuthenticatorData(authenticator), null);

            AssertMalformed(encoded);
        }

        [TestMethod]
        public void Parse_WithNoFormat_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            byte[] encoded = CborTestEncoder.EncodeAttestationObject(null, BuildAuthenticatorData(authenticator), CborTestEncoder.EncodeEmptyMap());

            WebAuthnParseException exception = AssertMalformed(encoded);
            StringAssert.Contains(exception.Message, "\"fmt\"");
        }

        [TestMethod]
        public void Parse_WithNoAuthenticatorData_Throws()
        {
            byte[] encoded = CborTestEncoder.EncodeAttestationObject("none", null, CborTestEncoder.EncodeEmptyMap());

            WebAuthnParseException exception = AssertMalformed(encoded);
            StringAssert.Contains(exception.Message, "\"authData\"");
        }

        [TestMethod]
        public void Parse_CarryingKeysThisLibraryDoesNotRead_IgnoresThem()
        {
            // CTAP authenticators add keys of their own — largeBlobKey and epAtt among them — and a
            // ceremony must not fail because one of them was present.
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            byte[] encoded = new CborMapBuilder()
                .With(CborTestEncoder.EncodeTextString("fmt"), CborTestEncoder.EncodeTextString("none"))
                .With(CborTestEncoder.EncodeTextString("attStmt"), CborTestEncoder.EncodeEmptyMap())
                .With(CborTestEncoder.EncodeTextString("largeBlobKey"), CborTestEncoder.EncodeByteString(new byte[] { 0x01, 0x02 }))
                .With(CborTestEncoder.EncodeInteger(7), CborTestEncoder.EncodeTextString("a key that is not a text string"))
                .With(CborTestEncoder.EncodeTextString("authData"), CborTestEncoder.EncodeByteString(BuildAuthenticatorData(authenticator)))
                .Build();

            AttestationObject parsed = AttestationObject.Parse(encoded);

            CollectionAssert.AreEqual(authenticator.CredentialId, parsed.AuthenticatorData.CredentialId);
        }

        [TestMethod]
        public void Parse_CarryingTheSameKeyTwiceUnderDifferentEncodings_Throws()
        {
            // CBOR Strict mode compares encoded key bytes, so "fmt" written minimally and non-minimally is
            // not a repeat to the reader. Without the parser's own guard the second "fmt" would win and a
            // packed attestation statement would sail through the format check as "none".
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            byte[] encoded = new CborMapBuilder()
                .With(CborTestEncoder.EncodeTextString("fmt"), CborTestEncoder.EncodeTextString("packed"))
                .With(CborTestEncoder.EncodeTextStringNonMinimally("fmt"), CborTestEncoder.EncodeTextString("none"))
                .With(CborTestEncoder.EncodeTextString("attStmt"), CborTestEncoder.EncodeEmptyMap())
                .With(CborTestEncoder.EncodeTextString("authData"), CborTestEncoder.EncodeByteString(BuildAuthenticatorData(authenticator)))
                .Build();

            WebAuthnParseException exception = AssertMalformed(encoded);
            StringAssert.Contains(exception.Message, "repeats the key \"fmt\"");
        }

        [TestMethod]
        public void Parse_FollowedByTrailingBytes_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            byte[] attestationObject = CborTestEncoder.EncodeAttestationObject("none", BuildAuthenticatorData(authenticator), CborTestEncoder.EncodeEmptyMap());

            byte[] withTrailingBytes = new byte[attestationObject.Length + 4];
            attestationObject.CopyTo(withTrailingBytes, 0);

            WebAuthnParseException exception = AssertMalformed(withTrailingBytes);
            StringAssert.Contains(exception.Message, "not part of it");
        }

        [TestMethod]
        public void Parse_AuthenticatorDataInsideItIsMalformed_ReportsThatRatherThanTheAttestationObject()
        {
            byte[] encoded = CborTestEncoder.EncodeAttestationObject("none", new byte[10], CborTestEncoder.EncodeEmptyMap());

            WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedAuthenticatorData, () => AttestationObject.Parse(encoded));
        }

        [TestMethod]
        public void Parse_BytesThatAreNotCbor_Throws()
        {
            AssertMalformed(new byte[] { 0xFF, 0xFF, 0xFF });
        }

        [TestMethod]
        public void Parse_CborThatIsNotAMap_Throws()
        {
            AssertMalformed(new byte[] { 0x01 });
        }

        private static byte[] BuildAuthenticatorData(SyntheticAuthenticator authenticator)
        {
            return new AuthenticatorDataBuilder
            {
                RelyingPartyIdHash = WebAuthnTestData.RelyingPartyIdHash,
                Aaguid = authenticator.Aaguid,
                CredentialId = authenticator.CredentialId,
                CredentialPublicKey = authenticator.EncodeCoseKey(),
            }.Build();
        }

        private static WebAuthnParseException AssertMalformed(byte[] encoded)
        {
            return WebAuthnParseAssert.Throws(WebAuthnParseError.MalformedAttestationObject, () => AttestationObject.Parse(encoded));
        }
    }
}
