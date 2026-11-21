using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// PKCE values (RFC 7636) as a client makes them: a random code verifier and its S256 challenge.
    /// </summary>
    public static class OAuthTestPkce
    {
        /// <summary>
        /// A new code verifier: 32 random bytes, base64url, 43 characters.
        /// </summary>
        /// <returns>The verifier.</returns>
        public static string CreateVerifier()
        {
            return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        }

        /// <summary>
        /// The S256 code challenge of a verifier: base64url of its SHA-256.
        /// </summary>
        /// <param name="codeVerifier">The verifier.</param>
        /// <returns>The challenge.</returns>
        public static string ChallengeFor(string codeVerifier)
        {
            ArgumentNullException.ThrowIfNull(codeVerifier);
            return Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }
    }
}
