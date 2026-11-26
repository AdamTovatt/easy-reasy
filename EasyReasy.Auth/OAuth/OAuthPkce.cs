using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// PKCE with S256 (RFC 7636): the only method the authorization server accepts, and the one a sign-in package's
    /// client side sends.
    /// </summary>
    public static class OAuthPkce
    {
        /// <summary>
        /// The length of an S256 code challenge: the base64url encoding of 32 bytes.
        /// </summary>
        public const int ChallengeLength = 43;

        /// <summary>
        /// The shortest code verifier the RFC allows.
        /// </summary>
        public const int MinVerifierLength = 43;

        /// <summary>
        /// The longest code verifier the RFC allows.
        /// </summary>
        public const int MaxVerifierLength = 128;

        /// <summary>
        /// Whether a PKCE code verifier hashes to a code challenge with S256 (RFC 7636 section 4.6), compared in constant
        /// time. A verifier outside the RFC's 43 to 128 unreserved characters never matches.
        /// </summary>
        /// <param name="codeVerifier">The token request's <c>code_verifier</c>.</param>
        /// <param name="codeChallenge">The authorization request's <c>code_challenge</c>.</param>
        /// <returns>Whether the verifier matches.</returns>
        public static bool VerifierMatchesChallenge(string codeVerifier, string codeChallenge)
        {
            if (!IsVerifier(codeVerifier))
            {
                return false;
            }

            string computed = CreateS256Challenge(codeVerifier);
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(codeChallenge));
        }

        /// <summary>
        /// The S256 code challenge for a code verifier (RFC 7636 section 4.2): the SHA-256 of its ASCII bytes, as unpadded
        /// base64url. A client makes the verifier with <see cref="SecureToken.Generate"/>, which is 43 unreserved
        /// characters.
        /// </summary>
        /// <param name="codeVerifier">The code verifier.</param>
        /// <returns>The code challenge.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="codeVerifier"/> is null.</exception>
        public static string CreateS256Challenge(string codeVerifier)
        {
            ArgumentNullException.ThrowIfNull(codeVerifier);
            return Base64UrlEncoding.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        /// <summary>
        /// Whether a value is a well-formed S256 code challenge: 43 base64url characters, the encoding of 32 bytes.
        /// </summary>
        /// <param name="codeChallenge">The value.</param>
        /// <returns>Whether it is well formed.</returns>
        public static bool IsS256Challenge(string codeChallenge)
        {
            return codeChallenge.Length == ChallengeLength && Base64UrlEncoding.IsCanonical(codeChallenge);
        }

        private static bool IsVerifier(string value)
        {
            if (value.Length < MinVerifierLength || value.Length > MaxVerifierLength)
            {
                return false;
            }

            foreach (char character in value)
            {
                bool unreserved = (character >= 'A' && character <= 'Z')
                    || (character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9')
                    || character == '-' || character == '.' || character == '_' || character == '~';
                if (!unreserved)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
