using System.Security.Cryptography;
using System.Text;
using static EasyReasy.Auth.Tests.SecretCipherTestKeys;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class AesGcmSecretCipherKeyringTests
    {
        [TestMethod]
        public void EncryptThenDecrypt_UnderEachHeldKey_RoundTrips()
        {
            SecretCipherKey[] keys = { NewKey(3), NewKey(7), NewKey(200) };
            byte[] plaintext = Encoding.UTF8.GetBytes("a totp secret");
            AesGcmSecretCipher reader = Keyring(keys, currentKeyId: 7);

            foreach (SecretCipherKey key in keys)
            {
                byte[] envelope = Keyring(keys, currentKeyId: key.Id).Encrypt(plaintext);

                Assert.AreEqual(key.Id, envelope[1]);
                CollectionAssert.AreEqual(plaintext, reader.Decrypt(envelope));
            }
        }

        [TestMethod]
        public void Encrypt_Keyring_StampsEnvelopeVersionThenCurrentKeyId()
        {
            // Key id 9, not 2, so the version byte and the key id cannot be confused for each other.
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1), NewKey(9) }, currentKeyId: 9);

            byte[] envelope = cipher.Encrypt(new byte[] { 1, 2, 3 });

            Assert.AreEqual((byte)2, cipher.EnvelopeVersion);
            Assert.AreEqual((byte?)9, cipher.CurrentKeyId);
            Assert.AreEqual(cipher.EnvelopeVersion, envelope[0]);
            Assert.AreEqual(cipher.CurrentKeyId, envelope[1]);
        }

        [TestMethod]
        public void EncryptThenDecrypt_EmptyPlaintext_RoundTrips()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);

            byte[] decrypted = cipher.Decrypt(cipher.Encrypt(Array.Empty<byte>()));

            Assert.AreEqual(0, decrypted.Length);
        }

        [TestMethod]
        public void Decrypt_VersionOneEnvelope_OpensWithTheNamedVersionOneKey()
        {
            byte[] oldKey = RandomNumberGenerator.GetBytes(32);
            byte[] envelope = new AesGcmSecretCipher(oldKey).Encrypt(Encoding.UTF8.GetBytes("written before rotation"));
            AesGcmSecretCipher cipher = Keyring(new[] { new SecretCipherKey(1, oldKey), NewKey(2) }, currentKeyId: 2, versionOneKeyId: 1);

            byte[] decrypted = cipher.Decrypt(envelope);

            Assert.AreEqual("written before rotation", Encoding.UTF8.GetString(decrypted));
        }

        [TestMethod]
        public void Decrypt_VersionOneEnvelope_WithNoVersionOneKeyNamed_Throws()
        {
            byte[] oldKey = RandomNumberGenerator.GetBytes(32);
            byte[] envelope = new AesGcmSecretCipher(oldKey).Encrypt(new byte[] { 1 });
            AesGcmSecretCipher cipher = Keyring(new[] { new SecretCipherKey(1, oldKey) }, currentKeyId: 1, versionOneKeyId: null);

            CryptographicException exception = Assert.ThrowsException<CryptographicException>(() => cipher.Decrypt(envelope));
            StringAssert.Contains(exception.Message, "version 1");
        }

        [TestMethod]
        public void Decrypt_KeyIdSwappedForAnotherHeldKey_FailsTheTagCheck()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1), NewKey(2) }, currentKeyId: 1);
            byte[] envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("secret"));
            envelope[1] = 2;

            Assert.ThrowsException<AuthenticationTagMismatchException>(() => cipher.Decrypt(envelope));
        }

        [TestMethod]
        public void Decrypt_UnknownKeyId_ThrowsNamingTheId()
        {
            byte[] envelope = Keyring(new[] { NewKey(42) }, currentKeyId: 42).Encrypt(Encoding.UTF8.GetBytes("secret"));
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1), NewKey(2) }, currentKeyId: 2);

            CryptographicException exception = Assert.ThrowsException<CryptographicException>(() => cipher.Decrypt(envelope));
            StringAssert.Contains(exception.Message, "key id 42");
        }

        [TestMethod]
        public void Decrypt_VersionByteDowngradedToOne_FailsTheTagCheck()
        {
            SecretCipherKey key = NewKey(1);
            AesGcmSecretCipher cipher = Keyring(new[] { key }, currentKeyId: 1, versionOneKeyId: 1);
            byte[] envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("secret"));

            // Rewrite a version 2 envelope as version 1 by dropping the key id: the same nonce, tag and
            // ciphertext under the same key, so only the associated data differs.
            byte[] downgraded = new byte[envelope.Length - 1];
            downgraded[0] = 1;
            envelope.AsSpan(2).CopyTo(downgraded.AsSpan(1));

            Assert.ThrowsException<AuthenticationTagMismatchException>(() => cipher.Decrypt(downgraded));
        }

        [TestMethod]
        public void Decrypt_TamperedCiphertext_FailsTheTagCheck()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);
            byte[] envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("secret"));
            envelope[^1] ^= 0xFF;

            Assert.ThrowsException<AuthenticationTagMismatchException>(() => cipher.Decrypt(envelope));
        }

        [TestMethod]
        public void Decrypt_VersionTwoEnvelopeShorterThanItsHeader_Throws()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);
            byte[] envelope = new byte[29]; // long enough for a version 1 header, one byte short for version 2
            envelope[0] = 2;
            envelope[1] = 1;

            CryptographicException exception = Assert.ThrowsException<CryptographicException>(() => cipher.Decrypt(envelope));
            StringAssert.Contains(exception.Message, "too short");
        }

        [TestMethod]
        public void Decrypt_UnsupportedVersionByte_Throws()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);
            byte[] envelope = cipher.Encrypt(new byte[] { 9 });
            envelope[0] = 3;

            CryptographicException exception = Assert.ThrowsException<CryptographicException>(() => cipher.Decrypt(envelope));
            StringAssert.Contains(exception.Message, "version 3");
        }

        [TestMethod]
        public void Constructor_ClonesKeyBytes_SoLaterMutationOfCallerArrayHasNoEffect()
        {
            byte[] key = RandomNumberGenerator.GetBytes(32);
            AesGcmSecretCipher cipher = Keyring(new[] { new SecretCipherKey(1, key) }, currentKeyId: 1);
            byte[] envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("secret"));

            Array.Clear(key);

            Assert.AreEqual("secret", Encoding.UTF8.GetString(cipher.Decrypt(envelope)));
        }

        [TestMethod]
        public void Constructor_ValidKeyring_Constructs()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(0), NewKey(255) }, currentKeyId: 255, versionOneKeyId: 0);

            Assert.AreEqual((byte?)255, cipher.CurrentKeyId);
        }

        [TestMethod]
        public void Constructor_NullKeys_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new AesGcmSecretCipher(null!, 1, null));
        }

        [TestMethod]
        public void SecretCipherKey_NullKey_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new SecretCipherKey(1, null!));
        }

        [TestMethod]
        public void Constructor_EmptyKeyring_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Keyring(Array.Empty<SecretCipherKey>(), currentKeyId: 1));
            StringAssert.Contains(exception.Message, "at least one key");
        }

        [TestMethod]
        public void Constructor_NullEntry_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Keyring(new[] { NewKey(1), null! }, currentKeyId: 1));
            StringAssert.Contains(exception.Message, "null at index 1");
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(16)]
        [DataRow(31)]
        [DataRow(33)]
        [DataRow(64)]
        public void Constructor_KeyNotExactly32Bytes_Throws(int keyLength)
        {
            SecretCipherKey wrongLength = new SecretCipherKey(4, new byte[keyLength]);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Keyring(new[] { NewKey(1), wrongLength }, currentKeyId: 1));
            StringAssert.Contains(exception.Message, $"Key 4 must be exactly 32 bytes for AES-256; got {keyLength}.");
        }

        [TestMethod]
        public void Constructor_DuplicateKeyId_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Keyring(new[] { NewKey(5), NewKey(5) }, currentKeyId: 5));
            StringAssert.Contains(exception.Message, "Key id 5 is held more than once");
        }

        [TestMethod]
        public void Constructor_CurrentKeyIdNotHeld_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Keyring(new[] { NewKey(1) }, currentKeyId: 9));
            StringAssert.Contains(exception.Message, "'currentKeyId' is 9");
        }

        [TestMethod]
        public void Constructor_VersionOneKeyIdNotHeld_Throws()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Keyring(new[] { NewKey(1) }, currentKeyId: 1, versionOneKeyId: 9));
            StringAssert.Contains(exception.Message, "'versionOneKeyId' is 9");
        }

        [TestMethod]
        public void Constructor_SeveralProblems_ReportsEveryOneAtOnce()
        {
            SecretCipherKey[] keys = { NewKey(5), NewKey(5), NewKey(5), new SecretCipherKey(6, new byte[16]) };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => Keyring(keys, currentKeyId: 8, versionOneKeyId: 9));

            StringAssert.Contains(exception.Message, "Key id 5 is held more than once");
            StringAssert.Contains(exception.Message, "Key 6 must be exactly 32 bytes");
            StringAssert.Contains(exception.Message, "'currentKeyId' is 8");
            StringAssert.Contains(exception.Message, "'versionOneKeyId' is 9");
            // A key id held three times is one problem, reported once.
            Assert.AreEqual(1, exception.Message.Split("Key id 5").Length - 1);
        }
    }
}
