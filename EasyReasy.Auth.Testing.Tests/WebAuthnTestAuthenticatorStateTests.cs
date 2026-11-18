namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// The authenticator's state belongs to it: arrays go in and out as copies, the transport list is
    /// checked when set, and a disposed authenticator refuses every ceremony alike.
    /// </summary>
    [TestClass]
    public class WebAuthnTestAuthenticatorStateTests
    {
        [TestMethod]
        public void Create_CallerChangesTheGivenId_CredentialIdIsUnchanged()
        {
            byte[] credentialId = new byte[] { 0x01, 0x02 };
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(credentialId: credentialId);

            credentialId[0] = 0xFF;

            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, authenticator.CredentialId);
        }

        [TestMethod]
        public void CredentialId_CallerChangesTheReturnedArray_CredentialIdIsUnchanged()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create(credentialId: new byte[] { 0x01, 0x02 });

            authenticator.CredentialId[0] = 0xFF;

            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, authenticator.CredentialId);
        }

        [TestMethod]
        public void UserHandle_CallerChangesTheArrayItSet_UserHandleIsUnchanged()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            byte[] userHandle = new byte[] { 0x01, 0x02 };
            authenticator.UserHandle = userHandle;

            userHandle[0] = 0xFF;

            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, authenticator.UserHandle);
        }

        [TestMethod]
        public void UserHandle_CallerChangesTheReturnedArray_UserHandleIsUnchanged()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.UserHandle = new byte[] { 0x01, 0x02 };

            byte[] returned = authenticator.UserHandle ?? throw new AssertFailedException("No user handle.");
            returned[0] = 0xFF;

            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, authenticator.UserHandle);
        }

        [TestMethod]
        public void Transports_CallerChangesTheArrayItSet_TransportsAreUnchanged()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            string[] transports = new[] { "usb" };
            authenticator.Transports = transports;

            transports[0] = "nfc";

            CollectionAssert.AreEqual(new[] { "usb" }, authenticator.Transports.ToArray());
        }

        [TestMethod]
        public void Transports_Read_IsNotTheArrayItIsStoredIn()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            Assert.IsNotInstanceOfType(authenticator.Transports, typeof(string[]));
        }

        [TestMethod]
        public void Transports_SetNull_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            Assert.ThrowsException<ArgumentNullException>(() => authenticator.Transports = null!);
        }

        [TestMethod]
        public void Transports_SetWithANullEntry_Throws()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();

            Assert.ThrowsException<ArgumentException>(() => authenticator.Transports = new string[] { "usb", null! });
        }

        [TestMethod]
        public void Register_EmptyTransports_ReportsNone()
        {
            using WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.Transports = Array.Empty<string>();

            WebAuthnRegisteredCredential credential = TestRelyingParty.RegisterSecondFactor(authenticator);

            Assert.AreEqual(0, credential.Transports?.Count ?? 0);
        }

        [TestMethod]
        public void Register_AfterDispose_ThrowsObjectDisposed()
        {
            WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => TestRelyingParty.RegisterSecondFactor(authenticator));
        }

        [TestMethod]
        public void Assert_AfterDispose_ThrowsObjectDisposed()
        {
            WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            TestRelyingParty.RegisterPasskey(authenticator);
            authenticator.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(
                () => authenticator.Assert(TestRelyingParty.NewGenerator().CreateDiscoverableAuthenticationOptions(), TestRelyingParty.Origin));
        }

        [TestMethod]
        public void Sign_AfterDispose_ThrowsObjectDisposed()
        {
            WebAuthnTestAuthenticator authenticator = WebAuthnTestAuthenticator.Create();
            authenticator.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => authenticator.Sign(new byte[] { 0x01 }));
        }
    }
}
