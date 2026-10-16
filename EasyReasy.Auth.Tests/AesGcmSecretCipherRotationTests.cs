using System.Security.Cryptography;
using System.Text;
using static EasyReasy.Auth.Tests.SecretCipherTestKeys;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class AesGcmSecretCipherRotationTests
    {
        private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("a totp secret");

        [TestMethod]
        public void IsStale_VersionOneEnvelope_IsStaleEvenWhenItsKeyIsCurrent()
        {
            byte[] key = NewKeyBytes();
            byte[] envelope = new AesGcmSecretCipher(key).Encrypt(Plaintext);
            AesGcmSecretCipher cipher = Keyring(new[] { new SecretCipherKey(1, key) }, currentKeyId: 1, versionOneKeyId: 1);

            Assert.IsTrue(cipher.IsStale(envelope));
        }

        [TestMethod]
        public void IsStale_SealedByAnotherHeldKey_IsTrue()
        {
            SecretCipherKey[] keys = { NewKey(1), NewKey(2) };
            byte[] envelope = Keyring(keys, currentKeyId: 1).Encrypt(Plaintext);

            Assert.IsTrue(Keyring(keys, currentKeyId: 2).IsStale(envelope));
        }

        [TestMethod]
        public void IsStale_SealedByTheCurrentKey_IsFalse()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);

            Assert.IsFalse(cipher.IsStale(cipher.Encrypt(Plaintext)));
        }

        [TestMethod]
        public void IsStale_SealedByAKeyThisCipherDoesNotHold_IsTrue()
        {
            byte[] envelope = Keyring(new[] { NewKey(42) }, currentKeyId: 42).Encrypt(Plaintext);
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);

            Assert.IsTrue(cipher.IsStale(envelope));
        }

        [TestMethod]
        public void IsStale_VersionTwoEnvelopeShorterThanItsHeader_ThrowsTooShort()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);
            byte[] envelope = new byte[29]; // long enough for a version 1 header, one byte short for version 2
            envelope[0] = 2;
            envelope[1] = 1;

            CryptographicException exception = Assert.ThrowsException<CryptographicException>(() => cipher.IsStale(envelope));
            StringAssert.Contains(exception.Message, "too short");
        }

        [TestMethod]
        public void IsStale_UnsupportedVersion_ThrowsNamingTheVersion()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);
            byte[] envelope = new byte[40];
            envelope[0] = 3;

            CryptographicException exception = Assert.ThrowsException<CryptographicException>(() => cipher.IsStale(envelope));
            StringAssert.Contains(exception.Message, "version 3");
        }

        [TestMethod]
        public void IsStale_SingleKeyCipher_VersionOneIsCurrentAndVersionTwoIsStale()
        {
            byte[] key = NewKeyBytes();
            AesGcmSecretCipher singleKey = new AesGcmSecretCipher(key);
            byte[] versionTwo = Keyring(new[] { new SecretCipherKey(1, key) }, currentKeyId: 1).Encrypt(Plaintext);

            Assert.IsFalse(singleKey.IsStale(singleKey.Encrypt(Plaintext)));
            Assert.IsTrue(singleKey.IsStale(versionTwo));
        }

        [TestMethod]
        public void ReEncrypt_SealedByAnOldKey_MovesTheValueToTheCurrentKey()
        {
            SecretCipherKey oldKey = NewKey(1);
            SecretCipherKey newKey = NewKey(9);
            byte[] envelope = Keyring(new[] { oldKey, newKey }, currentKeyId: 1).Encrypt(Plaintext);
            AesGcmSecretCipher cipher = Keyring(new[] { oldKey, newKey }, currentKeyId: 9);

            byte[] reEncrypted = cipher.ReEncrypt(envelope);

            Assert.AreEqual((byte)9, reEncrypted[1]);
            Assert.IsFalse(cipher.IsStale(reEncrypted));
            // The old key can now be dropped: a cipher holding only the new key opens the value.
            CollectionAssert.AreEqual(Plaintext, Keyring(new[] { newKey }, currentKeyId: 9).Decrypt(reEncrypted));
        }

        [TestMethod]
        public void ReEncrypt_VersionOneEnvelope_MovesTheValueToVersionTwoUnderTheCurrentKey()
        {
            byte[] oldKeyBytes = NewKeyBytes();
            byte[] envelope = new AesGcmSecretCipher(oldKeyBytes).Encrypt(Plaintext);
            SecretCipherKey newKey = NewKey(9);
            AesGcmSecretCipher cipher = Keyring(new[] { new SecretCipherKey(1, oldKeyBytes), newKey }, currentKeyId: 9, versionOneKeyId: 1);

            byte[] reEncrypted = cipher.ReEncrypt(envelope);

            Assert.AreEqual((byte)2, reEncrypted[0]);
            Assert.AreEqual((byte)9, reEncrypted[1]);
            CollectionAssert.AreEqual(Plaintext, Keyring(new[] { newKey }, currentKeyId: 9).Decrypt(reEncrypted));
        }

        [TestMethod]
        public void ReEncrypt_VersionOneEnvelopeWhoseKeyIsCurrent_MovesItToVersionTwoSoVersionOneCanBeRetired()
        {
            byte[] keyBytes = NewKeyBytes();
            byte[] envelope = new AesGcmSecretCipher(keyBytes).Encrypt(Plaintext);
            SecretCipherKey key = new SecretCipherKey(7, keyBytes);

            byte[] reEncrypted = Keyring(new[] { key }, currentKeyId: 7, versionOneKeyId: 7).ReEncrypt(envelope);

            Assert.AreEqual((byte)2, reEncrypted[0]);
            Assert.AreEqual((byte)7, reEncrypted[1]);
            CollectionAssert.AreEqual(Plaintext, Keyring(new[] { key }, currentKeyId: 7, versionOneKeyId: null).Decrypt(reEncrypted));
        }

        [TestMethod]
        public void ReEncrypt_SameStaleEnvelopeTwice_DrawsAFreshNonceEachTime()
        {
            SecretCipherKey[] keys = { NewKey(1), NewKey(9) };
            byte[] envelope = Keyring(keys, currentKeyId: 1).Encrypt(Plaintext);
            AesGcmSecretCipher cipher = Keyring(keys, currentKeyId: 9);

            byte[] first = cipher.ReEncrypt(envelope);
            byte[] second = cipher.ReEncrypt(envelope);

            CollectionAssert.AreNotEqual(first.AsSpan(2, 12).ToArray(), second.AsSpan(2, 12).ToArray());
        }

        [TestMethod]
        public void ReEncrypt_AlreadyUnderTheCurrentKey_ComesBackUnchanged()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);
            byte[] envelope = cipher.Encrypt(Plaintext);

            CollectionAssert.AreEqual(envelope, cipher.ReEncrypt(envelope));
        }

        [TestMethod]
        public void ReEncrypt_TamperedEnvelopeUnderTheCurrentKey_Throws()
        {
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);
            byte[] envelope = cipher.Encrypt(Plaintext);
            envelope[^1] ^= 0xFF;

            Assert.ThrowsException<AuthenticationTagMismatchException>(() => cipher.ReEncrypt(envelope));
        }

        [TestMethod]
        public void ReEncrypt_UnknownKeyId_ThrowsNamingTheId()
        {
            byte[] envelope = Keyring(new[] { NewKey(42) }, currentKeyId: 42).Encrypt(Plaintext);
            AesGcmSecretCipher cipher = Keyring(new[] { NewKey(1) }, currentKeyId: 1);

            CryptographicException exception = Assert.ThrowsException<CryptographicException>(() => cipher.ReEncrypt(envelope));
            StringAssert.Contains(exception.Message, "key id 42");
        }

        [TestMethod]
        public void ReEncrypt_FullRotationWalkthrough_RetiresTheOldKey()
        {
            byte[] oldKeyBytes = NewKeyBytes();
            SecretCipherKey oldKey = new SecretCipherKey(1, oldKeyBytes);
            SecretCipherKey newKey = NewKey(9);

            // Before rotation: one key, version 1 values.
            List<byte[]> stored = new List<byte[]> { new AesGcmSecretCipher(oldKeyBytes).Encrypt(Plaintext) };

            // Step 1: hold the new key everywhere, still sealing with the old one.
            AesGcmSecretCipher holding = Keyring(new[] { oldKey, newKey }, currentKeyId: 1, versionOneKeyId: 1);
            stored.Add(holding.Encrypt(Plaintext));

            // Step 2: make the new key current. Values written by step 1 instances still open.
            AesGcmSecretCipher rotated = Keyring(new[] { oldKey, newKey }, currentKeyId: 9, versionOneKeyId: 1);
            stored.Add(rotated.Encrypt(Plaintext));
            foreach (byte[] envelope in stored)
            {
                CollectionAssert.AreEqual(Plaintext, holding.Decrypt(envelope));
                CollectionAssert.AreEqual(Plaintext, rotated.Decrypt(envelope));
            }

            // Step 3: re-encrypt every stale value in the background.
            for (int index = 0; index < stored.Count; index++)
            {
                if (rotated.IsStale(stored[index]))
                {
                    stored[index] = rotated.ReEncrypt(stored[index]);
                }
            }

            // Step 4: drop the old key. Nothing stored needs it.
            AesGcmSecretCipher retired = Keyring(new[] { newKey }, currentKeyId: 9, versionOneKeyId: null);
            foreach (byte[] envelope in stored)
            {
                Assert.IsFalse(retired.IsStale(envelope));
                CollectionAssert.AreEqual(Plaintext, retired.Decrypt(envelope));
            }
        }

        [TestMethod]
        public void CurrentKeyId_SingleKeyCipher_IsNull()
        {
            Assert.IsNull(new AesGcmSecretCipher(NewKeyBytes()).CurrentKeyId);
        }

        [TestMethod]
        public void Decrypt_SingleKeyCipher_OpensAVersionTwoEnvelopeSealedByItsKey_WhateverTheKeyId()
        {
            byte[] key = NewKeyBytes();
            byte[] envelope = Keyring(new[] { new SecretCipherKey(7, key) }, currentKeyId: 7).Encrypt(Plaintext);

            CollectionAssert.AreEqual(Plaintext, new AesGcmSecretCipher(key).Decrypt(envelope));
        }

        [TestMethod]
        public void Decrypt_SingleKeyCipher_VersionTwoEnvelopeWithItsKeyIdRewritten_FailsTheTagCheck()
        {
            byte[] key = NewKeyBytes();
            byte[] envelope = Keyring(new[] { new SecretCipherKey(7, key) }, currentKeyId: 7).Encrypt(Plaintext);
            envelope[1] = 8;

            // Same key, so only the authenticated key id stands between this and a successful decrypt.
            Assert.ThrowsException<AuthenticationTagMismatchException>(() => new AesGcmSecretCipher(key).Decrypt(envelope));
        }

        [TestMethod]
        public void Decrypt_SingleKeyCipher_VersionTwoEnvelopeSealedByAnotherKey_FailsTheTagCheck()
        {
            byte[] envelope = Keyring(new[] { NewKey(7) }, currentKeyId: 7).Encrypt(Plaintext);

            Assert.ThrowsException<AuthenticationTagMismatchException>(() => new AesGcmSecretCipher(NewKeyBytes()).Decrypt(envelope));
        }

        [TestMethod]
        public void ReEncrypt_SingleKeyCipher_VersionTwoEnvelope_ComesBackAsVersionOne()
        {
            byte[] key = NewKeyBytes();
            byte[] envelope = Keyring(new[] { new SecretCipherKey(7, key) }, currentKeyId: 7).Encrypt(Plaintext);
            AesGcmSecretCipher singleKey = new AesGcmSecretCipher(key);

            byte[] reEncrypted = singleKey.ReEncrypt(envelope);

            Assert.AreEqual((byte)1, reEncrypted[0]);
            Assert.IsFalse(singleKey.IsStale(reEncrypted));
            CollectionAssert.AreEqual(Plaintext, singleKey.Decrypt(reEncrypted));
        }

        [TestMethod]
        public void ReEncrypt_SingleKeyCipher_ItsOwnVersionOneEnvelope_ComesBackUnchanged()
        {
            AesGcmSecretCipher singleKey = new AesGcmSecretCipher(NewKeyBytes());
            byte[] envelope = singleKey.Encrypt(Plaintext);

            CollectionAssert.AreEqual(envelope, singleKey.ReEncrypt(envelope));
        }
    }
}
