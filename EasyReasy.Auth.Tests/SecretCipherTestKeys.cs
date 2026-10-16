using System.Security.Cryptography;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Builds random keys and keyring ciphers for the <see cref="AesGcmSecretCipher"/> tests.
    /// </summary>
    internal static class SecretCipherTestKeys
    {
        public static byte[] NewKeyBytes()
        {
            return RandomNumberGenerator.GetBytes(32);
        }

        public static SecretCipherKey NewKey(byte id)
        {
            return new SecretCipherKey(id, NewKeyBytes());
        }

        public static AesGcmSecretCipher Keyring(IEnumerable<SecretCipherKey> keys, byte currentKeyId, byte? versionOneKeyId = null)
        {
            return new AesGcmSecretCipher(keys, currentKeyId, versionOneKeyId);
        }
    }
}
