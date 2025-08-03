namespace EasyReasy.Auth
{
    /// <summary>
    /// Interface for password hashing operations.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Creates a secure hash of the provided password using username as additional salt.
        /// </summary>
        /// <param name="password">The plain text password to hash.</param>
        /// <param name="username">The username to use as additional salt.</param>
        /// <returns>The hashed password.</returns>
        string HashPassword(string password, string username);

        /// <summary>
        /// Validates a password against a stored hash using username as additional salt.
        /// </summary>
        /// <param name="password">The plain text password to validate.</param>
        /// <param name="passwordHash">The stored password hash.</param>
        /// <param name="username">The username used during hashing.</param>
        /// <returns>True if the password matches the hash, false otherwise.</returns>
        bool ValidatePassword(string password, string passwordHash, string username);
    }
}