namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the reason an API key authentication attempt failed.
    /// Intended to flow into audit logs (e.g. ISO 27001 A.12.4.1 records of failed authentication attempts)
    /// — never returned to the client.
    /// </summary>
    public enum ApiKeyAuthFailureReason
    {
        /// <summary>
        /// No API key is registered matching the supplied value.
        /// </summary>
        UnknownKey,

        /// <summary>
        /// The API key was previously registered but has been revoked.
        /// </summary>
        KeyRevoked,

        /// <summary>
        /// The API key has passed its configured expiration time.
        /// </summary>
        KeyExpired,

        /// <summary>
        /// The API key authentication failed for a consumer-specific reason that does not fit the other categories.
        /// </summary>
        Other,
    }
}
