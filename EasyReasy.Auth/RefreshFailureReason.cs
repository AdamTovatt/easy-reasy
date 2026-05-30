namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the reason a refresh token operation failed.
    /// </summary>
    public enum RefreshFailureReason
    {
        /// <summary>
        /// The refresh token was not found in the store.
        /// </summary>
        TokenNotFound,

        /// <summary>
        /// The refresh token has expired.
        /// </summary>
        TokenExpired,

        /// <summary>
        /// The refresh token has been invalidated (e.g., the entire token family was revoked).
        /// </summary>
        TokenInvalidated,

        /// <summary>
        /// The refresh token has already been consumed, indicating potential token theft.
        /// The entire token family has been invalidated as a security measure.
        /// </summary>
        TheftDetected,

        /// <summary>
        /// The consumer-supplied <see cref="IRefreshClaimsResolver"/> denied the refresh
        /// (for example because the user's password has expired, the account has been
        /// disabled, or the role required for the session was revoked). The stored refresh
        /// token is not consumed, so a subsequent legitimate refresh after the deny condition
        /// clears is not affected.
        /// </summary>
        DeniedByResolver
    }
}
