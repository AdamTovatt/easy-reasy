using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Turns a <see cref="MicrosoftAuthState"/> into the OAuth <c>state</c> string and back: JSON, sealed with a
    /// dedicated <see cref="AesGcmSecretCipher"/>, then base64url. GCM authenticates as it decrypts, so a tampered or
    /// foreign state is unreadable rather than wrong.
    /// </summary>
    /// <remarks>
    /// The cipher is built from <see cref="MicrosoftAuthOptions.StateKey"/> and never resolved from DI: the
    /// application's registered <see cref="ISecretCipher"/> may be the one protecting its MFA secrets, and one key
    /// serves one purpose.
    /// </remarks>
    internal sealed class MicrosoftAuthStateProtector
    {
        private readonly AesGcmSecretCipher _cipher;

        /// <summary>
        /// Initializes the protector with its own cipher.
        /// </summary>
        /// <param name="stateKey">The 32-byte state key.</param>
        public MicrosoftAuthStateProtector(byte[] stateKey)
        {
            _cipher = new AesGcmSecretCipher(stateKey);
        }

        /// <summary>
        /// Seals a state.
        /// </summary>
        /// <param name="state">The state to seal.</param>
        /// <returns>The base64url <c>state</c> string.</returns>
        public string Protect(MicrosoftAuthState state)
        {
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(state);
            try
            {
                return Base64Url.EncodeToString(_cipher.Encrypt(plaintext));
            }
            finally
            {
                // The plaintext holds the PKCE verifier.
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        /// <summary>
        /// Opens a state string, or returns null when it is not one this protector sealed intact.
        /// </summary>
        /// <param name="protectedState">The <c>state</c> string from the redirect.</param>
        /// <returns>The state, or null when it is empty, not base64url, tampered with, sealed under another key, or
        /// not a complete state.</returns>
        public MicrosoftAuthState? Unprotect(string? protectedState)
        {
            if (string.IsNullOrEmpty(protectedState) || !Base64Url.IsValid(protectedState))
            {
                return null;
            }

            byte[] plaintext;
            try
            {
                plaintext = _cipher.Decrypt(Base64Url.DecodeFromChars(protectedState));
            }
            catch (CryptographicException)
            {
                return null;
            }

            try
            {
                // Only reachable for bytes this server sealed, so a parse failure means a state from an older layout.
                return JsonSerializer.Deserialize<MicrosoftAuthState>(plaintext);
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
