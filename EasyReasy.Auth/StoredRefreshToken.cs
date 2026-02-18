namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents a refresh token stored in the consumer's persistence layer.
    /// This model is passed to and returned from <see cref="IRefreshTokenStore"/> implementations.
    /// </summary>
    public sealed class StoredRefreshToken
    {
        /// <summary>
        /// The SHA-256 hash of the raw refresh token. Used as the primary lookup key.
        /// </summary>
        public required string TokenHash { get; init; }

        /// <summary>
        /// The subject (user identifier) this refresh token was issued for.
        /// </summary>
        public required string Subject { get; init; }

        /// <summary>
        /// The authentication type (e.g., "apikey" or "user") used when the token was originally issued.
        /// </summary>
        public required string AuthType { get; init; }

        /// <summary>
        /// The token family identifier. All rotated tokens in a chain share the same family ID.
        /// Used for theft detection — when a consumed token is reused, the entire family is invalidated.
        /// </summary>
        public required string FamilyId { get; init; }

        /// <summary>
        /// The UTC time when this refresh token was created.
        /// </summary>
        public required DateTime CreatedAt { get; init; }

        /// <summary>
        /// The UTC time when this refresh token expires.
        /// </summary>
        public required DateTime ExpiresAt { get; init; }

        /// <summary>
        /// The UTC time when this refresh token was consumed (used to obtain a new token pair).
        /// Null if the token has not yet been consumed.
        /// </summary>
        public DateTime? ConsumedAt { get; set; }

        /// <summary>
        /// Whether this refresh token has been invalidated (e.g., due to theft detection).
        /// </summary>
        public bool IsInvalidated { get; set; }

        /// <summary>
        /// JSON-serialized additional claims to include in the access token upon refresh.
        /// Null if no additional claims were provided.
        /// </summary>
        public string? SerializedClaims { get; init; }

        /// <summary>
        /// JSON-serialized roles to include in the access token upon refresh.
        /// Null if no roles were provided.
        /// </summary>
        public string? SerializedRoles { get; init; }
    }
}
