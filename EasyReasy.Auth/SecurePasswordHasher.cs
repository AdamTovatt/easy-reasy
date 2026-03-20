using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Secure password hasher using PBKDF2 with HMAC-SHA512.
    /// Produces V4 format hashes with a 128-bit random salt and 256-bit output.
    /// </summary>
    public class SecurePasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 128 / 8; // 128 bits
        private const int HashSize = 256 / 8; // 256 bits
        private const int Iterations = 100000;
        private const KeyDerivationPrf Prf = KeyDerivationPrf.HMACSHA512;
        private const int MaxPasswordByteLength = 1024;
        private const int MinimumIterationCount = 10000;

        /// <summary>
        /// Creates a secure hash of the provided password using PBKDF2 with HMAC-SHA512.
        /// </summary>
        /// <param name="password">The plain text password to hash.</param>
        /// <returns>The hashed password as a base64-encoded string in V4 format.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="password"/> is null, empty, whitespace, or exceeds 1024 UTF-8 bytes.
        /// </exception>
        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be null, empty, or whitespace.", nameof(password));
            }

            if (Encoding.UTF8.GetByteCount(password) > MaxPasswordByteLength)
            {
                throw new ArgumentException(
                    $"Password cannot exceed {MaxPasswordByteLength} UTF-8 bytes.",
                    nameof(password));
            }

            byte[] hashBytes = HashPasswordV4(password);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Validates a password against a stored hash.
        /// Only V4 format hashes (marker <c>0x02</c>) are accepted.
        /// </summary>
        /// <param name="password">The plain text password to validate.</param>
        /// <param name="passwordHash">The stored password hash.</param>
        /// <returns>True if the password matches the hash, false otherwise.</returns>
        public bool ValidatePassword(string password, string passwordHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            {
                return false;
            }

            if (Encoding.UTF8.GetByteCount(password) > MaxPasswordByteLength)
            {
                return false;
            }

            try
            {
                byte[] decodedHashedPassword = Convert.FromBase64String(passwordHash);

                if (decodedHashedPassword.Length == 0)
                {
                    return false;
                }

                if (decodedHashedPassword[0] != 0x02) // Only V4 format accepted
                {
                    return false;
                }

                return VerifyHashedPasswordV4(decodedHashedPassword, password);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a V4 format hash using PBKDF2 with HMAC-SHA512.
        /// Format: { 0x02, prf (UInt32), iter count (UInt32), salt length (UInt32), salt, subkey }
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <returns>The hash bytes in V4 format.</returns>
        private static byte[] HashPasswordV4(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            byte[] subkey = KeyDerivation.Pbkdf2(password, salt, Prf, Iterations, HashSize);

            byte[] outputBytes = new byte[13 + salt.Length + subkey.Length];
            outputBytes[0] = 0x02; // V4 format marker
            WriteNetworkByteOrder(outputBytes, 1, (uint)Prf);
            WriteNetworkByteOrder(outputBytes, 5, (uint)Iterations);
            WriteNetworkByteOrder(outputBytes, 9, (uint)salt.Length);
            Buffer.BlockCopy(salt, 0, outputBytes, 13, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 13 + salt.Length, subkey.Length);

            return outputBytes;
        }

        /// <summary>
        /// Verifies a V4 format hash against a password.
        /// Rejects hashes with an iteration count below the minimum threshold.
        /// </summary>
        /// <param name="hashedPassword">The hashed password bytes.</param>
        /// <param name="password">The password to verify.</param>
        /// <returns>True if the password matches, false otherwise.</returns>
        private static bool VerifyHashedPasswordV4(byte[] hashedPassword, string password)
        {
            try
            {
                KeyDerivationPrf prf = (KeyDerivationPrf)ReadNetworkByteOrder(hashedPassword, 1);
                int iterCount = (int)ReadNetworkByteOrder(hashedPassword, 5);
                int saltLength = (int)ReadNetworkByteOrder(hashedPassword, 9);

                if (iterCount < MinimumIterationCount)
                {
                    return false;
                }

                if (saltLength < 128 / 8)
                {
                    return false;
                }

                byte[] salt = hashedPassword.AsSpan(13, saltLength).ToArray();

                int subkeyLength = hashedPassword.Length - 13 - salt.Length;
                if (subkeyLength < 128 / 8)
                {
                    return false;
                }

                byte[] expectedSubkey = new byte[subkeyLength];
                Buffer.BlockCopy(hashedPassword, 13 + salt.Length, expectedSubkey, 0, expectedSubkey.Length);

                byte[] actualSubkey = KeyDerivation.Pbkdf2(password, salt, prf, iterCount, subkeyLength);

                return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer from the specified position in a byte array using network byte order.
        /// </summary>
        /// <param name="buffer">The byte array to read from.</param>
        /// <param name="offset">The offset to read from.</param>
        /// <returns>The 32-bit unsigned integer.</returns>
        private static uint ReadNetworkByteOrder(byte[] buffer, int offset)
        {
            return ((uint)buffer[offset + 0] << 24)
                | ((uint)buffer[offset + 1] << 16)
                | ((uint)buffer[offset + 2] << 8)
                | ((uint)buffer[offset + 3]);
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer to the specified position in a byte array using network byte order.
        /// </summary>
        /// <param name="buffer">The byte array to write to.</param>
        /// <param name="offset">The offset to write to.</param>
        /// <param name="value">The 32-bit unsigned integer to write.</param>
        private static void WriteNetworkByteOrder(byte[] buffer, int offset, uint value)
        {
            buffer[offset + 0] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)(value >> 0);
        }
    }
}
