namespace EasyReasy.Auth
{
    /// <summary>
    /// Interface for generating and validating password reset tokens.
    /// The handler manages the cryptographic operations; the consumer is responsible
    /// for storage, expiration enforcement, and delivery (e.g., email).
    /// </summary>
    public interface IPasswordResetTokenHandler
    {
        /// <summary>
        /// Generates a cryptographically secure password reset token pair.
        /// The <see cref="PasswordResetToken.Token"/> should be sent to the user (e.g., via email),
        /// and the <see cref="PasswordResetToken.TokenHash"/> should be stored in the database.
        /// </summary>
        /// <returns>A <see cref="PasswordResetToken"/> containing the plaintext token and its hash.</returns>
        PasswordResetToken GenerateResetToken();

        /// <summary>
        /// Validates a plaintext reset token provided by the user against a stored token hash.
        /// </summary>
        /// <param name="token">The plaintext token provided by the user.</param>
        /// <param name="storedTokenHash">The SHA-256 hash retrieved from the database.</param>
        /// <returns>True if the token matches the stored hash, false otherwise.</returns>
        bool ValidateResetToken(string token, string storedTokenHash);
    }
}
