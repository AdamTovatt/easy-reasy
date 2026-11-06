using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The one way this package hashes a value: SHA-256 of its UTF-8 bytes, as unpadded base64url. That is both the
    /// PKCE S256 code challenge (a verifier is base64url, so its UTF-8 and ASCII bytes are the same) and the binding
    /// cookie's hash in the state.
    /// </summary>
    internal static class MicrosoftHash
    {
        /// <summary>
        /// Hashes a value.
        /// </summary>
        /// <param name="value">The value to hash.</param>
        /// <returns>The SHA-256 digest as unpadded base64url.</returns>
        public static string Sha256(string value)
        {
            return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }
    }
}
