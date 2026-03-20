namespace EasyReasy.Auth
{
    /// <summary>
    /// Interface for password hashing operations using PBKDF2.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Creates a secure hash of the provided password.
        /// </summary>
        /// <param name="password">The plain text password to hash.</param>
        /// <returns>The hashed password as a base64-encoded string.</returns>
        string HashPassword(string password);

        /// <summary>
        /// Validates a password against a stored hash.
        /// </summary>
        /// <param name="password">The plain text password to validate.</param>
        /// <param name="passwordHash">The stored password hash.</param>
        /// <returns>True if the password matches the hash, false otherwise.</returns>
        bool ValidatePassword(string password, string passwordHash);
    }
}
