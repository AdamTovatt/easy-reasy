namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Contains user information extracted from a validated Google ID token.
    /// </summary>
    public class GoogleUserInfo
    {
        /// <summary>
        /// Google's stable unique identifier for the user (the "sub" claim).
        /// </summary>
        public required string Subject { get; init; }

        /// <summary>
        /// The user's email address.
        /// </summary>
        public required string Email { get; init; }

        /// <summary>
        /// Whether Google has verified that the user owns this email address (the token's
        /// <c>email_verified</c> claim). An unverified email must not be trusted as an identifier.
        /// </summary>
        public required bool EmailVerified { get; init; }

        /// <summary>
        /// The user's display name, or null if not provided.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// The URL of the user's profile picture, or null if not provided.
        /// </summary>
        public string? PictureUrl { get; init; }
    }
}
