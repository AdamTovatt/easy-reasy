namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// What the credential reference holds on its own. Everything but a missing id is checked by the
    /// generator it is handed to, and is tested there.
    /// </summary>
    [TestClass]
    public class WebAuthnCredentialReferenceTests
    {
        [TestMethod]
        public void Constructor_WhatRegistrationReported_KeepsBothValues()
        {
            string[] transports = new[] { "internal", "hybrid" };

            WebAuthnCredentialReference reference = new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, transports);

            Assert.AreEqual(WebAuthnOptionsTestData.FirstCredentialId, reference.CredentialId);
            CollectionAssert.AreEqual(transports, reference.Transports!.ToArray());
        }

        [TestMethod]
        public void Constructor_NoTransports_LeavesThemNull()
        {
            WebAuthnCredentialReference reference = new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId);

            Assert.IsNull(reference.Transports);
        }

        [TestMethod]
        public void Constructor_NullCredentialId_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => new WebAuthnCredentialReference(null!));

            Assert.AreEqual("credentialId", exception.ParamName);
        }
    }
}
