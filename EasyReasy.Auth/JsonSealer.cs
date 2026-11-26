using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Seals a value as text no one without its key can read or forge: the value's JSON, encrypted and authenticated
    /// with its own <see cref="AesGcmSecretCipher"/>, as unpadded base64url so it fits a URL, a cookie or a store
    /// column. A sign-in package seals what a flow must carry through the browser this way, such as an OAuth
    /// <c>state</c>.
    /// </summary>
    /// <remarks>
    /// Give each purpose its own key: never the key of a cipher the application registers for another purpose, such as
    /// encrypting MFA secrets, so a value sealed for one purpose cannot be presented as another's. Sealing says the value
    /// came from this key's holder, unchanged; it does not expire, so a type that must expire carries its own expiry and
    /// its reader checks it.
    /// </remarks>
    public sealed class JsonSealer
    {
        private readonly AesGcmSecretCipher _cipher;

        /// <summary>
        /// Initializes the sealer with its own key.
        /// </summary>
        /// <param name="key">The 32-byte (AES-256) key, copied now.</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentException">The key is not exactly 32 bytes.</exception>
        public JsonSealer(byte[] key)
        {
            _cipher = new AesGcmSecretCipher(key);
        }

        /// <summary>
        /// Seals a value.
        /// </summary>
        /// <typeparam name="T">The value's type, serialized with <see cref="JsonSerializer"/>'s defaults.</typeparam>
        /// <param name="value">The value to seal.</param>
        /// <returns>The sealed value as unpadded base64url.</returns>
        public string Seal<T>(T value)
        {
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(value);
            try
            {
                return Base64Url.EncodeToString(_cipher.Encrypt(plaintext));
            }
            finally
            {
                // The plaintext may hold a secret, such as a PKCE verifier.
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        /// <summary>
        /// Opens a sealed value, or returns null when it is not one this key sealed intact.
        /// </summary>
        /// <typeparam name="T">The type it was sealed as.</typeparam>
        /// <param name="sealedValue">The sealed value.</param>
        /// <returns>The value, or null when it is null or empty, not base64url, tampered with, sealed under another key,
        /// or not a <typeparamref name="T"/> (such as one sealed from an older layout of the type).</returns>
        public T? Open<T>(string? sealedValue)
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
