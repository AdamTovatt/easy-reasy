using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Secure password hasher using PBKDF2 with HMAC-SHA512, following ASP.NET Core Identity patterns.
    /// Supports versioned hash formats and automatic rehashing.
    /// </summary>
    public class SecurePasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 128 / 8; // 128 bits
        private const int HashSize = 256 / 8; // 256 bits
        private const int Iterations = 100000; // High iteration count for security
        private const KeyDerivationPrf Prf = KeyDerivationPrf.HMACSHA512; // Better than SHA256

        /// <summary>
        /// Creates a secure hash of the provided password using PBKDF2 with HMAC-SHA512.
        /// </summary>
        /// <param name="password">The plain text password to hash.</param>
        /// <param name="username">The username to use as additional salt.</param>
        /// <returns>The hashed password in binary format.</returns>
        public string HashPassword(string password, string username)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be null, empty, or whitespace.", nameof(password));
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be null, empty, or whitespace.", nameof(username));
            }

            // Combine password with username as additional salt
            string passwordWithUsername = password + username;

            // Generate hash using V3 format
            byte[] hashBytes = HashPasswordV3(passwordWithUsername);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Validates a password against a stored hash.
        /// </summary>
        /// <param name="password">The plain text password to validate.</param>
        /// <param name="passwordHash">The stored password hash.</param>
        /// <param name="username">The username used during hashing.</param>
        /// <returns>True if the password matches the hash, false otherwise.</returns>
        public bool ValidatePassword(string password, string passwordHash, string username)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(username))
            {
                return false;
            }

            try
            {
                // Combine password with username as additional salt
                string passwordWithUsername = password + username;

                byte[] decodedHashedPassword = Convert.FromBase64String(passwordHash);

                // Read the format marker from the hashed password
                if (decodedHashedPassword.Length == 0)
                {
                    return false;
                }

                switch (decodedHashedPassword[0])
                {
                    case 0x00: // V2 format (legacy)
                        return VerifyHashedPasswordV2(decodedHashedPassword, passwordWithUsername);

                    case 0x01: // V3 format (current)
                        return VerifyHashedPasswordV3(decodedHashedPassword, passwordWithUsername);

                    default:
                        return false; // Unknown format marker
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a V3 format hash using PBKDF2 with HMAC-SHA512.
        /// Format: { 0x01, prf (UInt32), iter count (UInt32), salt length (UInt32), salt, subkey }
        /// </summary>
        /// <param name="password">The password to hash.</param>
        /// <returns>The hash bytes in V3 format.</returns>
        private static byte[] HashPasswordV3(string password)
        {
            // Generate a random salt
            byte[] salt = new byte[SaltSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Generate the hash using PBKDF2 with HMAC-SHA512
            byte[] subkey = KeyDerivation.Pbkdf2(password, salt, Prf, Iterations, HashSize);

            // Create the output bytes in V3 format
            byte[] outputBytes = new byte[13 + salt.Length + subkey.Length];
            outputBytes[0] = 0x01; // V3 format marker
            WriteNetworkByteOrder(outputBytes, 1, (uint)Prf);
            WriteNetworkByteOrder(outputBytes, 5, (uint)Iterations);
            WriteNetworkByteOrder(outputBytes, 9, (uint)salt.Length);
            Buffer.BlockCopy(salt, 0, outputBytes, 13, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 13 + salt.Length, subkey.Length);

            return outputBytes;
        }

        /// <summary>
        /// Verifies a V2 format hash (for backward compatibility).
        /// </summary>
        /// <param name="hashedPassword">The hashed password bytes.</param>
        /// <param name="password">The password to verify.</param>
        /// <returns>True if the password matches, false otherwise.</returns>
        private static bool VerifyHashedPasswordV2(byte[] hashedPassword, string password)
        {
            const KeyDerivationPrf Pbkdf2Prf = KeyDerivationPrf.HMACSHA1; // V2 used SHA1
            const int Pbkdf2IterCount = 1000; // V2 used 1000 iterations
            const int Pbkdf2SubkeyLength = 256 / 8; // 256 bits
            const int SaltSize = 128 / 8; // 128 bits

            // We know ahead of time the exact length of a valid V2 hashed password payload.
            if (hashedPassword.Length != 1 + SaltSize + Pbkdf2SubkeyLength)
            {
                return false; // Bad size
            }

            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(hashedPassword, 1, salt, 0, salt.Length);

            byte[] expectedSubkey = new byte[Pbkdf2SubkeyLength];
            Buffer.BlockCopy(hashedPassword, 1 + salt.Length, expectedSubkey, 0, expectedSubkey.Length);

            // Hash the incoming password and verify it
            byte[] actualSubkey = KeyDerivation.Pbkdf2(password, salt, Pbkdf2Prf, Pbkdf2IterCount, Pbkdf2SubkeyLength);

            return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
        }

        /// <summary>
        /// Verifies a V3 format hash.
        /// </summary>
        /// <param name="hashedPassword">The hashed password bytes.</param>
        /// <param name="password">The password to verify.</param>
        /// <returns>True if the password matches, false otherwise.</returns>
        private static bool VerifyHashedPasswordV3(byte[] hashedPassword, string password)
        {
            try
            {
                // Read header information
                KeyDerivationPrf prf = (KeyDerivationPrf)ReadNetworkByteOrder(hashedPassword, 1);
                int iterCount = (int)ReadNetworkByteOrder(hashedPassword, 5);
                int saltLength = (int)ReadNetworkByteOrder(hashedPassword, 9);

                // Read the salt: must be >= 128 bits
                if (saltLength < 128 / 8)
                {
                    return false;
                }

                byte[] salt = hashedPassword.AsSpan(13, saltLength).ToArray();

                // Read the subkey (the rest of the payload): must be >= 128 bits
                int subkeyLength = hashedPassword.Length - 13 - salt.Length;
                if (subkeyLength < 128 / 8)
                {
                    return false;
                }

                byte[] expectedSubkey = new byte[subkeyLength];
                Buffer.BlockCopy(hashedPassword, 13 + salt.Length, expectedSubkey, 0, expectedSubkey.Length);

                // Hash the incoming password and verify it
                byte[] actualSubkey = KeyDerivation.Pbkdf2(password, salt, prf, iterCount, subkeyLength);

                return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
            }
            catch
            {
                // This should never occur except in the case of a malformed payload, where
                // we might go off the end of the array. Regardless, a malformed payload
                // implies verification failed.
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