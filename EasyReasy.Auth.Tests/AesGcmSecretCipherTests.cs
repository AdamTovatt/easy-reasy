using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class AesGcmSecretCipherTests
    {
        private static byte[] NewKey()
        {
            return RandomNumberGenerator.GetBytes(32);
        }

        [TestMethod]
        public void EncryptThenDecrypt_RoundTripsPlaintext()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(NewKey());
            byte[] plaintext = Encoding.UTF8.GetBytes("a totp secret");

            byte[] envelope = cipher.Encrypt(plaintext);
            byte[] decrypted = cipher.Decrypt(envelope);

            CollectionAssert.AreEqual(plaintext, decrypted);
        }

        [TestMethod]
        public void Encrypt_ProducesDifferentCiphertextEachTime_DueToRandomNonce()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(NewKey());
            byte[] plaintext = Encoding.UTF8.GetBytes("same input");

            byte[] first = cipher.Encrypt(plaintext);
            byte[] second = cipher.Encrypt(plaintext);

            CollectionAssert.AreNotEqual(first, second);
            // Both still decrypt back to the same plaintext.
            CollectionAssert.AreEqual(plaintext, cipher.Decrypt(first));
            CollectionAssert.AreEqual(plaintext, cipher.Decrypt(second));
        }

        [TestMethod]
        public void EnvelopeVersion_IsStampedAsLeadingEnvelopeByte()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(NewKey());
            byte[] envelope = cipher.Encrypt(new byte[] { 1, 2, 3 });
            Assert.AreEqual(cipher.EnvelopeVersion, envelope[0]);
        }

        [TestMethod]
        public void Decrypt_TamperedCiphertext_Throws()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(NewKey());
            byte[] envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("secret"));
            envelope[^1] ^= 0xFF; // flip a ciphertext byte

            Assert.ThrowsException<AuthenticationTagMismatchException>(() => cipher.Decrypt(envelope));
        }

        [TestMethod]
        public void Decrypt_WithWrongKey_Throws()
        {
            byte[] plaintext = Encoding.UTF8.GetBytes("secret");
            byte[] envelope = new AesGcmSecretCipher(NewKey()).Encrypt(plaintext);

            AesGcmSecretCipher otherCipher = new AesGcmSecretCipher(NewKey());
            Assert.ThrowsException<AuthenticationTagMismatchException>(() => otherCipher.Decrypt(envelope));
        }

        [TestMethod]
        public void Decrypt_UnsupportedVersionByte_Throws()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(NewKey());
            byte[] envelope = cipher.Encrypt(new byte[] { 9 });
            envelope[0] = 0x7F;

            Assert.ThrowsException<CryptographicException>(() => cipher.Decrypt(envelope));
        }

        [TestMethod]
        public void Decrypt_TooShortEnvelope_Throws()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(NewKey());
            Assert.ThrowsException<CryptographicException>(() => cipher.Decrypt(new byte[] { 1, 2, 3 }));
        }

        [DataTestMethod]
        [DataRow(16)]
        [DataRow(31)]
        [DataRow(64)]
        public void Constructor_NonAes256Key_Throws(int keyLength)
        {
            Assert.ThrowsException<ArgumentException>(() => new AesGcmSecretCipher(new byte[keyLength]));
        }

        [TestMethod]
        public void Constructor_NullKey_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new AesGcmSecretCipher(null!));
        }

        [TestMethod]
        public void EncryptThenDecrypt_EmptyPlaintext_RoundTrips()
        {
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(NewKey());

            byte[] envelope = cipher.Encrypt(Array.Empty<byte>());
            byte[] decrypted = cipher.Decrypt(envelope);

            Assert.AreEqual(0, decrypted.Length);
        }

        [TestMethod]
        public void Constructor_ClonesKey_SoLaterMutationOfCallerArrayHasNoEffect()
        {
            byte[] key = NewKey();
            AesGcmSecretCipher cipher = new AesGcmSecretCipher(key);
            byte[] envelope = cipher.Encrypt(Encoding.UTF8.GetBytes("secret"));

            // Mutating the caller's array after construction must not affect the cipher's internal key;
            // if the constructor kept a reference instead of a clone, this decrypt would fail the tag.
            Array.Clear(key);

            byte[] decrypted = cipher.Decrypt(envelope);
            Assert.AreEqual("secret", Encoding.UTF8.GetString(decrypted));
        }
    }
}
