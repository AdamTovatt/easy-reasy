using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The library's bearer secrets (refresh tokens, password reset tokens, the OAuth server's request ids, codes,
    /// client ids and secrets, flow binding cookies, and a sign-in package's PKCE verifiers and nonces): how a new one is
    /// generated and the one hash it is stored under.
    /// </summary>
    public static class SecureToken
    {
        /// <summary>
        /// A new 256-bit random value, base64url-encoded.
        /// </summary>
        /// <returns>The value.</returns>
        public static string Generate()
        {
            return Base64UrlEncoding.Encode(RandomNumberGenerator.GetBytes(32));
        }

        /// <summary>
        /// The SHA-256 hash of a value's UTF-8 bytes.
        /// </summary>
        /// <param name="value">The raw value.</param>
        /// <returns>The hash bytes.</returns>
        public static byte[] HashBytes(string value)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// The SHA-256 hash of a value, lowercase hex, as a store keeps it.
        /// </summary>
        /// <param name="value">The raw value.</param>
        /// <returns>The hash.</returns>
        public static string Hash(string value)
        {
            return Convert.ToHexString(HashBytes(value)).ToLowerInvariant();
        }

        /// <summary>
        /// Whether a presented value hashes to a stored lowercase-hex hash, compared in constant time.
        /// </summary>
        /// <param name="presented">The raw value presented.</param>
        /// <param name="storedHash">The stored hash.</param>
        /// <returns>Whether they match.</returns>
        public static bool MatchesHash(string presented, string storedHash)
        {
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(presented)), Encoding.ASCII.GetBytes(storedHash));
        }
    }
}
