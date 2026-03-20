using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Secure implementation of <see cref="IPasswordResetTokenHandler"/> using
    /// cryptographically random tokens and SHA-256 hashing.
    /// This class is stateless and thread-safe; register as singleton.
    /// </summary>
    public class SecurePasswordResetTokenHandler : IPasswordResetTokenHandler
    {
        private const int TokenByteLength = 32; // 256 bits

        /// <summary>
        /// Generates a cryptographically secure password reset token pair.
        /// The token is a 256-bit random value encoded as base64url.
        /// The hash is the lowercase hexadecimal SHA-256 digest of the token.
        /// </summary>
        /// <returns>A <see cref="PasswordResetToken"/> containing the plaintext token and its hash.</returns>
        public PasswordResetToken GenerateResetToken()
        {
            byte[] tokenBytes = RandomNumberGenerator.GetBytes(TokenByteLength);

            string token = Convert.ToBase64String(tokenBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            string tokenHash = HashToken(token);

            return new PasswordResetToken
            {
                Token = token,
                TokenHash = tokenHash
            };
        }

        /// <summary>
        /// Validates a plaintext reset token against a stored token hash by
        /// computing the SHA-256 hash of the token and comparing it to the stored hash.
        /// </summary>
        /// <param name="token">The plaintext token provided by the user.</param>
        /// <param name="storedTokenHash">The SHA-256 hash retrieved from the database.</param>
        /// <returns>True if the token matches the stored hash, false otherwise.</returns>
        public bool ValidateResetToken(string token, string storedTokenHash)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(storedTokenHash))
            {
                return false;
            }

            byte[] computedHashBytes = HashTokenBytes(token);
            byte[] storedHashBytes;

            try
            {
                storedHashBytes = Convert.FromHexString(storedTokenHash);
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(computedHashBytes, storedHashBytes);
        }

        /// <summary>
        /// Computes the SHA-256 hash of a token string and returns the raw hash bytes.
        /// </summary>
        /// <param name="token">The token to hash.</param>
        /// <returns>The SHA-256 hash bytes.</returns>
        private static byte[] HashTokenBytes(string token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            return SHA256.HashData(bytes);
        }

        /// <summary>
        /// Computes the SHA-256 hash of a token string.
        /// </summary>
        /// <param name="token">The token to hash.</param>
        /// <returns>The lowercase hexadecimal SHA-256 hash.</returns>
        private static string HashToken(string token)
        {
            return Convert.ToHexString(HashTokenBytes(token)).ToLowerInvariant();
        }
    }
}
