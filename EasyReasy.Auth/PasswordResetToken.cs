namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents a password reset token pair: a plaintext token to send to the user
    /// and a SHA-256 hash to store in the database. Never store the plaintext token.
    /// </summary>
    public readonly struct PasswordResetToken
    {
        /// <summary>
        /// The plaintext token to send to the user (base64url-encoded, 256-bit random).
        /// This value should be included in a reset link and never stored directly.
        /// </summary>
        public required string Token { get; init; }

        /// <summary>
        /// The SHA-256 hash of the token, to be stored in the database.
        /// Used to verify the token when the user returns with it.
        /// </summary>
        public required string TokenHash { get; init; }
    }
}
