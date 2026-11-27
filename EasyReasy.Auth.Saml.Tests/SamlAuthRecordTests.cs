namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// <see cref="SamlAuthRecord"/> refuses a record no store could key or return: an unknown kind, or a blank hash or
    /// sealed value.
    /// </summary>
    [TestClass]
    public class SamlAuthRecordTests
    {
        private static readonly DateTime ExpiresAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void Constructor_KindThatIsNoMember_Throws()
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new SamlAuthRecord((SamlAuthRecordKind)3, "hash", "sealed", ExpiresAt));

            Assert.AreEqual("kind", exception.ParamName);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public void Constructor_BlankHash_Throws(string hash)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new SamlAuthRecord(SamlAuthRecordKind.Completion, hash, "sealed", ExpiresAt));

            Assert.AreEqual("hash", exception.ParamName);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public void Constructor_BlankSealedValue_Throws(string sealedValue)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new SamlAuthRecord(SamlAuthRecordKind.PendingRequest, "hash", sealedValue, ExpiresAt));

            Assert.AreEqual("sealedValue", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_NullHashOrSealedValue_Throws()
        {
            ArgumentNullException nullHash = Assert.ThrowsException<ArgumentNullException>(
                () => new SamlAuthRecord(SamlAuthRecordKind.Completion, null!, "sealed", ExpiresAt));
            ArgumentNullException nullSealedValue = Assert.ThrowsException<ArgumentNullException>(
                () => new SamlAuthRecord(SamlAuthRecordKind.Completion, "hash", null!, ExpiresAt));

            Assert.AreEqual("hash", nullHash.ParamName);
            Assert.AreEqual("sealedValue", nullSealedValue.ParamName);
        }

        [TestMethod]
        public void Constructor_ValidRecord_KeepsEveryField()
        {
            SamlAuthRecord record = new SamlAuthRecord(SamlAuthRecordKind.PendingRequest, "hash", "sealed", ExpiresAt);

            Assert.AreEqual(SamlAuthRecordKind.PendingRequest, record.Kind);
            Assert.AreEqual("hash", record.Hash);
            Assert.AreEqual("sealed", record.SealedValue);
            Assert.AreEqual(ExpiresAt, record.ExpiresAt);
        }
    }
}
