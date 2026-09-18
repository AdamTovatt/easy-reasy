using System.Buffers.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class PublicKeyCredentialUserEntityTests
    {
        [TestMethod]
        public void Constructor_EncodesTheHandleAsBase64UrlAndNotBase64()
        {
            // These three bytes are chosen so the two alphabets disagree on every character: standard
            // base64 gives "+/+/", base64url gives "-_-_". A handle round-tripped through the wrong one
            // would be a credential the browser cannot match.
            byte[] handle = new byte[] { 0xFB, 0xFF, 0xBF };

            PublicKeyCredentialUserEntity user = new PublicKeyCredentialUserEntity(handle, "ada@example.com", "Ada Lovelace");

            Assert.AreEqual("-_-_", user.Id);
        }

        [TestMethod]
        public void Constructor_DistinctNames_KeepsThemApart()
        {
            // Distinct values, neither a substring of the other, so a transposition fails this.
            PublicKeyCredentialUserEntity user = new PublicKeyCredentialUserEntity(new byte[] { 0x01 }, "ada@example.com", "Ada Lovelace");

            Assert.AreEqual("ada@example.com", user.Name);
            Assert.AreEqual("Ada Lovelace", user.DisplayName);
        }

        [TestMethod]
        public void Constructor_HandleOfTheMaximumLength_IsAccepted()
        {
            PublicKeyCredentialUserEntity user = new PublicKeyCredentialUserEntity(new byte[64], "ada@example.com", "Ada Lovelace");

            Assert.AreEqual(64, Base64Url.DecodeFromChars(user.Id).Length);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(65)]
        public void Constructor_HandleOutsideTheAllowedLength_Throws(int handleLength)
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new PublicKeyCredentialUserEntity(new byte[handleLength], "ada@example.com", "Ada Lovelace"));

            Assert.AreEqual("id", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_EmptyName_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new PublicKeyCredentialUserEntity(new byte[] { 0x01 }, "   ", "Ada Lovelace"));

            Assert.AreEqual("name", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_EmptyDisplayName_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new PublicKeyCredentialUserEntity(new byte[] { 0x01 }, "ada@example.com", "   "));

            Assert.AreEqual("displayName", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("id")]
        [DataRow("name")]
        [DataRow("displayName")]
        public void Constructor_NullArgument_NamesTheArgument(string nullArgumentName)
        {
            byte[]? id = nullArgumentName == "id" ? null : new byte[] { 0x01 };
            string? name = nullArgumentName == "name" ? null : "ada@example.com";
            string? displayName = nullArgumentName == "displayName" ? null : "Ada Lovelace";

            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => new PublicKeyCredentialUserEntity(id!, name!, displayName!));

            Assert.AreEqual(nullArgumentName, exception.ParamName);
        }

        [TestMethod]
        public void Constructor_NameLongerThanAnAuthenticatorMustStore_Throws()
        {
            // An oversized value otherwise fails inside CTAP, with an error the application cannot pin on
            // any one of the three fields.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new PublicKeyCredentialUserEntity(new byte[] { 0x01 }, new string('a', 65), "Ada Lovelace"));

            Assert.AreEqual("name", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_DisplayNameLongerThanAnAuthenticatorMustStore_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new PublicKeyCredentialUserEntity(new byte[] { 0x01 }, "ada@example.com", new string('a', 65)));

            Assert.AreEqual("displayName", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_MultiByteNameWithinTheByteLimit_IsAccepted()
        {
            // The limit is on UTF-8 bytes, not characters: 32 of these are 64 bytes.
            PublicKeyCredentialUserEntity user = new PublicKeyCredentialUserEntity(new byte[] { 0x01 }, new string('ä', 32), "Ada Lovelace");

            Assert.AreEqual(new string('ä', 32), user.Name);
        }

        [TestMethod]
        public void Constructor_MultiByteNameOverTheByteLimit_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new PublicKeyCredentialUserEntity(new byte[] { 0x01 }, new string('ä', 33), "Ada Lovelace"));

            Assert.AreEqual("name", exception.ParamName);
        }
    }
}
