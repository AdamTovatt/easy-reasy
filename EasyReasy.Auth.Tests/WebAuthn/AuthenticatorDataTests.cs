namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class AuthenticatorDataTests
    {
        [TestMethod]
        public void Parse_MinimalHeader_ReadsTheRelyingPartyIdHash()
        {
            AuthenticatorData parsed = AuthenticatorData.Parse(NewBuilder().Build());

            CollectionAssert.AreEqual(WebAuthnTestData.RelyingPartyIdHash, parsed.RelyingPartyIdHash);
        }

        [TestMethod]
        public void Parse_SignCount_IsReadBigEndian()
        {
            // 0x01020304 read the other way round would be 67305985, so the value proves the byte order.
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.SignCount = 0x01020304;

            AuthenticatorData parsed = AuthenticatorData.Parse(builder.Build());

            Assert.AreEqual(16909060u, parsed.SignCount);
        }

        [TestMethod]
        public void Parse_UserPresentOnly_ReportsPresentAndNotVerified()
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.UserPresent = true;
            builder.UserVerified = false;

            AuthenticatorData parsed = AuthenticatorData.Parse(builder.Build());

            Assert.IsTrue(parsed.UserPresent);
            Assert.IsFalse(parsed.UserVerified);
        }

        [TestMethod]
        public void Parse_UserVerifiedOnly_ReportsVerifiedAndNotPresent()
        {
            // The mirror of the test above: together they fail if the two flag bits are ever transposed.
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.UserPresent = false;
            builder.UserVerified = true;

            AuthenticatorData parsed = AuthenticatorData.Parse(builder.Build());

            Assert.IsFalse(parsed.UserPresent);
            Assert.IsTrue(parsed.UserVerified);
        }

        [TestMethod]
        public void Parse_BackupEligibleOnly_ReportsEligibleAndNotBackedUp()
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.BackupEligible = true;
            builder.BackedUp = false;

            AuthenticatorData parsed = AuthenticatorData.Parse(builder.Build());

            Assert.IsTrue(parsed.BackupEligible);
            Assert.IsFalse(parsed.BackedUp);
        }

        [TestMethod]
        public void Parse_BackupEligibleAndBackedUp_ReportsBoth()
        {
            // With the mirror above, this pins the two backup bits apart the same way the user-presence
            // pair does — a transposition would make one of the two tests fail.
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.BackupEligible = true;
            builder.BackedUp = true;

            AuthenticatorData parsed = AuthenticatorData.Parse(builder.Build());

            Assert.IsTrue(parsed.BackupEligible);
            Assert.IsTrue(parsed.BackedUp);
        }

        [TestMethod]
        public void Parse_BackedUpWithoutBeingBackupEligible_Throws()
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.BackupEligible = false;
            builder.BackedUp = true;

            WebAuthnParseException exception = AssertParseError(builder.Build());
            StringAssert.Contains(exception.Message, "not eligible for backup");
        }

        [TestMethod]
        public void Parse_HeaderOnly_CarriesNoAttestedCredentialData()
        {
            AuthenticatorData parsed = AuthenticatorData.Parse(NewBuilder().Build());

            Assert.IsNull(parsed.Aaguid);
            Assert.IsNull(parsed.CredentialId);
            Assert.IsNull(parsed.CredentialPublicKey);
        }

        [TestMethod]
        public void Parse_AssertionShapedData_ReadsTheHeaderWithNoAttestedCredentialData()
        {
            // The shape an authentication assertion produces: no attested credential data, and extension
            // outputs present. This is what the feature spends its life parsing.
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.UserPresent = true;
            builder.UserVerified = true;
            builder.SignCount = 42;
            builder.Extensions = CborTestEncoder.EncodeEmptyMap();

            AuthenticatorData parsed = AuthenticatorData.Parse(builder.Build());

            Assert.AreEqual(42u, parsed.SignCount);
            Assert.IsTrue(parsed.UserVerified);
            Assert.IsNull(parsed.CredentialId);
            Assert.IsNull(parsed.CredentialPublicKey);
            Assert.IsNull(parsed.Aaguid);
        }

        [TestMethod]
        public void Parse_AttestedCredentialData_ReadsAaguidCredentialIdAndPublicKey()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            byte[] coseKey = authenticator.EncodeCoseKey();
            Guid aaguid = new Guid("08987058-cadc-4b81-b6e1-30de50dcbe96");

            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = aaguid;
            builder.CredentialId = authenticator.CredentialId;
            builder.CredentialPublicKey = coseKey;

            AuthenticatorData parsed = AuthenticatorData.Parse(builder.Build());

            // The AAGUID is written big-endian; reading it the way Guid lays itself out natively would
            // scramble the first three fields, so the round trip proves the byte order.
            Assert.AreEqual(aaguid, parsed.Aaguid);
            CollectionAssert.AreEqual(authenticator.CredentialId, parsed.CredentialId);
            CollectionAssert.AreEqual(coseKey, parsed.CredentialPublicKey);
        }

        [TestMethod]
        public void Parse_AttestedCredentialData_ReadsWhateverCredentialIdTheAuthenticatorReports()
        {
            // Two authenticators with different credential ids read back as themselves, which is what the
            // stored credential is looked up by.
            byte[] firstCredentialId = new byte[] { 0x11, 0x22, 0x33 };
            byte[] secondCredentialId = new byte[] { 0x44, 0x55, 0x66, 0x77 };

            using SyntheticAuthenticator first = SyntheticAuthenticator.Create(CoseAlgorithm.Es256, firstCredentialId);
            using SyntheticAuthenticator second = SyntheticAuthenticator.Create(CoseAlgorithm.Es256, secondCredentialId);

            CollectionAssert.AreEqual(firstCredentialId, ParseAttestedCredentialData(first).CredentialId);
            CollectionAssert.AreEqual(secondCredentialId, ParseAttestedCredentialData(second).CredentialId);
        }

        [TestMethod]
        public void Parse_AttestedCredentialDataFollowedByExtensions_StillReadsThePublicKeyExactly()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();
            byte[] coseKey = authenticator.EncodeCoseKey();

            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = authenticator.Aaguid;
            builder.CredentialId = authenticator.CredentialId;
            builder.CredentialPublicKey = coseKey;
            builder.Extensions = CborTestEncoder.EncodeEmptyMap();

            AuthenticatorData parsed = AuthenticatorData.Parse(builder.Build());

            // The COSE key has no length prefix, so where it ends is only knowable by decoding it. If the
            // parser took "everything that is left" the extensions would be swallowed into the key.
            CollectionAssert.AreEqual(coseKey, parsed.CredentialPublicKey);
        }

        [TestMethod]
        public void Parse_ExtensionFlagSetButNoExtensionBytes_BlamesTheExtensions()
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Extensions = Array.Empty<byte>();

            WebAuthnParseException exception = AssertParseError(builder.Build());
            StringAssert.Contains(exception.Message, "extensions");
        }

        [TestMethod]
        public void Parse_ExtensionsThatAreNotCbor_BlamesTheExtensions()
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Extensions = new byte[] { 0xFF, 0xFF };

            WebAuthnParseException exception = AssertParseError(builder.Build());
            StringAssert.Contains(exception.Message, "extensions");
        }

        [TestMethod]
        public void Parse_ShorterThanTheFixedHeader_Throws()
        {
            // One byte short of the 37-byte fixed header.
            byte[] truncated = NewBuilder().Build()[..36];

            AssertParseError(truncated);
        }

        [TestMethod]
        public void Parse_AttestedCredentialDataFlagButNoRoomForIt_Throws()
        {
            byte[] header = NewBuilder().Build();
            header[32] |= 0x40;

            AssertParseError(header);
        }

        [TestMethod]
        public void Parse_CredentialIdLongerThanWhatFollowsIt_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = authenticator.Aaguid;
            builder.CredentialId = authenticator.CredentialId;
            builder.CredentialPublicKey = authenticator.EncodeCoseKey();
            // Deliberately under the 1023-byte cap so this exercises "longer than the bytes that follow"
            // rather than "longer than the spec allows"; raising it past 1023 silently moves the test to
            // the other branch, which is already covered below.
            builder.DeclaredCredentialIdLength = 1000;

            WebAuthnParseException exception = AssertParseError(builder.Build());
            StringAssert.Contains(exception.Message, "but carries only");
        }

        [TestMethod]
        public void Parse_CredentialIdLongerThanTheSpecAllows_Throws()
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = Guid.Empty;
            builder.CredentialId = new byte[1024];
            builder.CredentialPublicKey = CborTestEncoder.EncodeEmptyMap();

            WebAuthnParseException exception = AssertParseError(builder.Build());
            StringAssert.Contains(exception.Message, "1023 is the maximum");
        }

        [TestMethod]
        public void Parse_EmptyCredentialId_Throws()
        {
            // A credential stored under an empty id could never be found again.
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = Guid.Empty;
            builder.CredentialId = Array.Empty<byte>();
            builder.CredentialPublicKey = CborTestEncoder.EncodeEmptyMap();

            WebAuthnParseException exception = AssertParseError(builder.Build());
            StringAssert.Contains(exception.Message, "empty credential id");
        }

        [TestMethod]
        public void Parse_CredentialIdWithNoPublicKeyAfterIt_BlamesThePublicKey()
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = Guid.Empty;
            builder.CredentialId = new byte[] { 0x01 };

            WebAuthnParseException exception = AssertParseError(builder.Build());
            StringAssert.Contains(exception.Message, "credential public key");
        }

        [TestMethod]
        public void Parse_TrailingBytesNoFlagAccountsFor_Throws()
        {
            using SyntheticAuthenticator authenticator = SyntheticAuthenticator.Create();

            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = authenticator.Aaguid;
            builder.CredentialId = authenticator.CredentialId;
            builder.CredentialPublicKey = authenticator.EncodeCoseKey();
            builder.TrailingBytes = new byte[] { 0x01, 0x02 };

            AssertParseError(builder.Build());
        }

        [TestMethod]
        public void Parse_PublicKeyThatIsNotCbor_Throws()
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = Guid.Empty;
            builder.CredentialId = new byte[] { 0x01 };
            builder.CredentialPublicKey = new byte[] { 0xFF, 0xFF };

            AssertParseError(builder.Build());
        }

        private static AuthenticatorDataBuilder NewBuilder()
        {
            return new AuthenticatorDataBuilder { RelyingPartyIdHash = WebAuthnTestData.RelyingPartyIdHash };
        }

        private static AuthenticatorData ParseAttestedCredentialData(SyntheticAuthenticator authenticator)
        {
            AuthenticatorDataBuilder builder = NewBuilder();
            builder.Aaguid = authenticator.Aaguid;
            builder.CredentialId = authenticator.CredentialId;
            builder.CredentialPublicKey = authenticator.EncodeCoseKey();

            return AuthenticatorData.Parse(builder.Build());
        }

        private static WebAuthnParseException AssertParseError(byte[] data)
        {
            return WebAuthnTestData.AssertParseError(WebAuthnParseError.MalformedAuthenticatorData, () => AuthenticatorData.Parse(data));
        }
    }
}
