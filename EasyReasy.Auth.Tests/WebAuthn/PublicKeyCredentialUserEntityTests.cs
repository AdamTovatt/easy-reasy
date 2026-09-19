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
        public void Constructor_NameLongerThanAnAuthenticatorMustStore_IsAccepted()
        {
            // The 64-byte rule is the user handle's, and applying it here would reject valid input: a name
            // an authenticator cannot store in full is truncated by whoever cannot store it, not refused.
            // An email address of 65 bytes is an ordinary one, and its owner has to be able to enroll.
            PublicKeyCredentialUserEntity user = new PublicKeyCredentialUserEntity(
                new byte[] { 0x01 },
                new string('a', 65),
                new string('b', 200));

            Assert.AreEqual(new string('a', 65), user.Name);
            Assert.AreEqual(new string('b', 200), user.DisplayName);
        }

        [TestMethod]
        public void Constructor_MultiByteNameLongerThanSixtyFourBytes_IsAccepted()
        {
            // 33 of these are 66 bytes, so this is over the handle's limit whichever way it is counted.
            PublicKeyCredentialUserEntity user = new PublicKeyCredentialUserEntity(new byte[] { 0x01 }, new string('ä', 33), "Ada Lovelace");

            Assert.AreEqual(new string('ä', 33), user.Name);
        }

        [TestMethod]
        public void Constructor_UserHandleLongerThanTheSpecPermits_Throws()
        {
            // The handle keeps its hard limit: §5.4.3 states it as a MUST NOT, and an authenticator has
            // nowhere to put the excess.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => new PublicKeyCredentialUserEntity(new byte[PublicKeyCredentialUserEntity.MaximumIdLength + 1], "ada@example.com", "Ada Lovelace"));

            Assert.AreEqual("id", exception.ParamName);
        }

        [TestMethod]
        public void Constructor_UserHandleExactlyAtTheLimit_IsAccepted()
        {
            // The other half of the bound: without this the test above would pass against a limit of zero.
            PublicKeyCredentialUserEntity user = new PublicKeyCredentialUserEntity(
                new byte[PublicKeyCredentialUserEntity.MaximumIdLength],
                "ada@example.com",
                "Ada Lovelace");

            Assert.AreEqual(Base64UrlEncoding.Encode(new byte[PublicKeyCredentialUserEntity.MaximumIdLength]), user.Id);
        }
    }
}
