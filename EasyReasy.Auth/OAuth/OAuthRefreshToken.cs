namespace EasyReasy.Auth
{
    /// <summary>
    /// A refresh token issued to an OAuth client. It works as <see cref="StoredRefreshToken"/> does for a first-party
    /// session (rotation within a family, reuse invalidating the family) and adds the client it is bound to and the
    /// grant it was issued under. It is kept in <see cref="IOAuthServerStore"/>, apart from
    /// <see cref="IRefreshTokenStore"/>, so a first-party session policy such as
    /// <see cref="ConcurrentSessionPolicy.SingleSession"/> never reaches a client's connection and a client's refresh
    /// token is never redeemable at the first-party refresh endpoint.
    /// </summary>
    public sealed class OAuthRefreshToken
    {
        /// <summary>
        /// The SHA-256 hash (lowercase hex) of the raw refresh token. The primary lookup key.
        /// </summary>
        public required string TokenHash { get; init; }

        /// <summary>
        /// The family the token belongs to. Every token rotated from one authorization code shares it, and a token
        /// presented twice invalidates the whole family.
        /// </summary>
        public required string FamilyId { get; init; }

        /// <summary>
        /// The application's id for the grant the token was issued under (<see cref="OAuthGrant.GrantId"/>). Revoking
        /// the grant invalidates every family under it.
        /// </summary>
        public required string GrantId { get; init; }

        /// <summary>
        /// The client the token is bound to. Only that client can refresh or revoke it.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// The subject the token's access tokens are issued for.
        /// </summary>
        public required string Subject { get; init; }

        /// <summary>
        /// The authentication type the token's access tokens carry.
        /// </summary>
        public required string AuthType { get; init; }

        /// <summary>
        /// The claims the next access token is built from, serialized with <see cref="RefreshTokenClaims.SerializeClaims"/>,
        /// or null for none.
        /// </summary>
        public string? SerializedClaims { get; init; }

        /// <summary>
        /// The roles the next access token carries, serialized with <see cref="RefreshTokenClaims.SerializeRoles"/>, or
        /// null for none.
        /// </summary>
        public string? SerializedRoles { get; init; }

        /// <summary>
        /// The UTC time the token was issued.
        /// </summary>
        public required DateTime CreatedAt { get; init; }

        /// <summary>
        /// The UTC time the token expires.
        /// </summary>
        public required DateTime ExpiresAt { get; init; }

        /// <summary>
        /// The UTC time the token was used to obtain the next pair, or null while it has not been.
        /// </summary>
        public DateTime? ConsumedAt { get; set; }

        /// <summary>
        /// Whether the token's family or grant has been invalidated.
        /// </summary>
        public bool IsInvalidated { get; set; }
    }
}
