using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Pins both envelope layouts byte for byte against an independent AES-GCM computation, and pins
    /// version 1 against an envelope written by the cipher as it was before key rotation existed.
    /// </summary>
    [TestClass]
    public class AesGcmSecretCipherEnvelopeLayoutTests
    {
        // Written by AesGcmSecretCipher(byte[]) in EasyReasy.Auth 5.10.0, before key rotation existed,
        // with key bytes 0x00..0x1F and the plaintext below.
        private const string VersionOneEnvelopeFromBeforeRotation =
            "0159AF13B6960AC9E24EA2EC4F75D8100399BE7ABB982A2ABF2E79C31128F2813EF9EF1484C8C99776C9AB326E";

        private const string PlaintextSealedBeforeRotation = "sealed by 5.10.0";

        private static byte[] SequentialKey()
        {
            byte[] key = new byte[32];
            for (int index = 0; index < key.Length; index++)
            {
                key[index] = (byte)index;
            }

            return key;
        }

        /// <summary>
        /// Builds the envelope the layout describes from its parts: the prefix (version byte, then key
        /// id in version 2) as associated data, then nonce, tag and ciphertext.
        /// </summary>
        private static byte[] SealIndependently(byte[] key, byte[] prefix, ReadOnlySpan<byte> nonce, byte[] plaintext)
        {
            byte[] envelope = new byte[prefix.Length + 12 + 16 + plaintext.Length];
            prefix.CopyTo(envelope, 0);
            nonce.CopyTo(envelope.AsSpan(prefix.Length, 12));

            using AesGcm aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, envelope.AsSpan(prefix.Length + 28), envelope.AsSpan(prefix.Length + 12, 16), prefix);

            return envelope;
        }

        [TestMethod]
        public void VersionOneFixture_SealedIndependently_MatchesByteForByte()
        {
            byte[] envelope = Convert.FromHexString(VersionOneEnvelopeFromBeforeRotation);
            byte[] plaintext = Encoding.UTF8.GetBytes(PlaintextSealedBeforeRotation);

            byte[] expected = SealIndependently(SequentialKey(), new byte[] { 1 }, envelope.AsSpan(1, 12), plaintext);

            CollectionAssert.AreEqual(expected, envelope);
        }

        [TestMethod]
        public void SingleKeyEncrypt_WritesTheVersionOneLayoutByteForByte()
        {
            byte[] key = SequentialKey();
            byte[] plaintext = Encoding.UTF8.GetBytes(PlaintextSealedBeforeRotation);
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(key);

            byte[] envelope = cipher.Encrypt(plaintext);

            // The nonce is the only random part; given it, every other byte is fixed by the layout.
            byte[] expected = SealIndependently(key, new byte[] { 1 }, envelope.AsSpan(1, 12), plaintext);
            CollectionAssert.AreEqual(expected, envelope);
            Assert.AreEqual(Convert.FromHexString(VersionOneEnvelopeFromBeforeRotation).Length, envelope.Length);
        }

        [TestMethod]
        public void SingleKeyDecrypt_OpensAnEnvelopeWrittenBeforeRotation()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(SequentialKey());

            byte[] plaintext = cipher.Decrypt(Convert.FromHexString(VersionOneEnvelopeFromBeforeRotation));

            Assert.AreEqual(PlaintextSealedBeforeRotation, Encoding.UTF8.GetString(plaintext));
        }

        [TestMethod]
        public void KeyringDecrypt_OpensAnEnvelopeWrittenBeforeRotation_WithTheNamedVersionOneKey()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(
                new[] { new SecretCipherKey(1, SequentialKey()), new SecretCipherKey(2, RandomNumberGenerator.GetBytes(32)) },
                currentKeyId: 2,
                versionOneKeyId: 1);

            byte[] plaintext = cipher.Decrypt(Convert.FromHexString(VersionOneEnvelopeFromBeforeRotation));

            Assert.AreEqual(PlaintextSealedBeforeRotation, Encoding.UTF8.GetString(plaintext));
        }

        [TestMethod]
        public void KeyringEncrypt_WritesTheVersionTwoLayoutByteForByte_WithVersionAndKeyIdAsAssociatedData()
        {
            byte[] key = SequentialKey();
            byte[] plaintext = Encoding.UTF8.GetBytes("sealed under key 42");
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(
                new[] { new SecretCipherKey(42, key), new SecretCipherKey(7, RandomNumberGenerator.GetBytes(32)) },
                currentKeyId: 42,
                versionOneKeyId: null);

            byte[] envelope = cipher.Encrypt(plaintext);

            byte[] expected = SealIndependently(key, new byte[] { 2, 42 }, envelope.AsSpan(2, 12), plaintext);
            CollectionAssert.AreEqual(expected, envelope);
            Assert.AreEqual(2 + 12 + 16 + plaintext.Length, envelope.Length);
        }

        [TestMethod]
        public void SingleKeyEncrypt_DrawsAFreshNonceEveryTime()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(SequentialKey());
            HashSet<string> nonces = new HashSet<string>();

            for (int count = 0; count < 200; count++)
            {
                byte[] envelope = cipher.Encrypt(new byte[] { 1 });
                Assert.IsTrue(nonces.Add(Convert.ToHexString(envelope, 1, 12)), "A nonce was reused.");
            }
        }

        [TestMethod]
        public void KeyringEncrypt_DrawsAFreshNonceEveryTime()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(
                new[] { new SecretCipherKey(3, SequentialKey()) },
                currentKeyId: 3,
                versionOneKeyId: null);
            HashSet<string> nonces = new HashSet<string>();

            for (int count = 0; count < 200; count++)
            {
                byte[] envelope = cipher.Encrypt(new byte[] { 1 });
                Assert.IsTrue(nonces.Add(Convert.ToHexString(envelope, 2, 12)), "A nonce was reused.");
            }
        }
    }
}
