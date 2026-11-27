using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Seals what the package keeps in <see cref="ISamlAuthStore"/> with <see cref="SamlAuthOptions.StateKey"/>: encrypted
    /// and authenticated, so the store holds no identity, rule or caller binding in plaintext, and a value the package did
    /// not seal is refused.
    /// </summary>
    internal sealed class SamlSealer
    {
        private readonly AesGcmSecretCipher _cipher;

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlSealer"/> class.
        /// </summary>
        /// <param name="stateKey">The 32-byte key.</param>
        public SamlSealer(byte[] stateKey)
        {
            _cipher = new AesGcmSecretCipher(stateKey);
        }

        /// <summary>
        /// Seals a value as base64url text.
        /// </summary>
        public string Seal<T>(T value)
        {
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(value);
            try
            {
                return Base64Url.EncodeToString(_cipher.Encrypt(plaintext));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        /// <summary>
        /// Opens a sealed value, or returns null when it was not sealed by this key or does not hold a
        /// <typeparamref name="T"/>.
        /// </summary>
        public T? Open<T>(string sealedValue)
            where T : class
        {
            if (string.IsNullOrEmpty(sealedValue) || !Base64Url.IsValid(sealedValue))
            {
                return null;
            }

            byte[] plaintext;
            try
            {
                plaintext = _cipher.Decrypt(Base64Url.DecodeFromChars(sealedValue));
            }
            catch (CryptographicException)
            {
                return null;
            }

            try
            {
                // Only reachable for bytes this key sealed, so a parse failure means a value from an older layout.
                return JsonSerializer.Deserialize<T>(plaintext);
            }
            catch (JsonException)
            {
                return null;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }
}
