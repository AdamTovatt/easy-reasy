namespace EasyReasy.Auth
{
    /// <summary>
    /// An authorization code issued on an approval, waiting for the client to redeem it at the token endpoint. It holds
    /// everything the redemption checks and everything the tokens will carry.
    /// </summary>
    public sealed class OAuthAuthorizationCode
    {
        /// <summary>
        /// The SHA-256 hash (lowercase hex) of the raw code. The primary lookup key; the raw code is never stored.
        /// </summary>
        public required string CodeHash { get; init; }

        /// <summary>
        /// The client the code was issued to. Only that client can redeem it.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// The redirect URI of the authorization request. The token request must name the same one.
        /// </summary>
        public required string RedirectUri { get; init; }

        /// <summary>
        /// The PKCE code challenge (S256) of the authorization request.
        /// </summary>
        public required string CodeChallenge { get; init; }

        /// <summary>
        /// The <c>resource</c> of the authorization request, or null. A token request that names a resource must name
        /// this one.
        /// </summary>
        public string? Resource { get; init; }

        /// <summary>
        /// The application's id for the grant the approval recorded (<see cref="OAuthGrant.GrantId"/>), carried onto
        /// every refresh token issued under it.
        /// </summary>
        public required string GrantId { get; init; }

        /// <summary>
        /// The subject the tokens are issued for (<see cref="OAuthGrant.Subject"/>).
        /// </summary>
        public required string Subject { get; init; }

        /// <summary>
        /// The authentication type the tokens carry (<see cref="OAuthGrant.AuthType"/>).
        /// </summary>
        public required string AuthType { get; init; }

        /// <summary>
        /// The grant's claims, serialized with <see cref="RefreshTokenClaims.SerializeClaims"/>, or null for none.
        /// </summary>
        public string? SerializedClaims { get; init; }

        /// <summary>
        /// The grant's roles, serialized with <see cref="RefreshTokenClaims.SerializeRoles"/>, or null for none.
        /// </summary>
        public string? SerializedRoles { get; init; }

        /// <summary>
        /// The refresh token family the code's tokens get, chosen when the code is issued, so a second redemption of
        /// the code can invalidate whatever the first one issued.
        /// </summary>
        public required string FamilyId { get; init; }

        /// <summary>
        /// The UTC time after which the code can no longer be redeemed.
        /// </summary>
        public required DateTime ExpiresAt { get; init; }

        /// <summary>
        /// The UTC time the code was redeemed, or null while it has not been.
        /// </summary>
        public DateTime? RedeemedAt { get; set; }

        /// <summary>
        /// Whether the code was invalidated: its grant was revoked
        /// (<see cref="IOAuthServerStore.InvalidateGrantAuthorizationCodesAsync"/>), or it was presented a second time
        /// (<see cref="IOAuthServerStore.InvalidateAuthorizationCodeAsync"/>). An invalidated code issues no tokens, even
        /// to a redemption already under way.
        /// </summary>
        public bool IsInvalidated { get; set; }
    }
}
