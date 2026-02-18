namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the reason an external identity-provider authentication attempt failed
    /// (for example a Google, Apple, or Microsoft sign-in). Intended to flow into audit logs
    /// (ISO 27001 A.12.4.1 records of failed authentication attempts) — never returned to the client.
    /// </summary>
    public enum ExternalAuthFailureReason
    {
        /// <summary>
        /// The identity-provider token did not validate — for example an invalid signature, an expired
        /// token, an audience mismatch, or an identity whose hosted domain/tenant is not allowed.
        /// </summary>
        InvalidToken,

        /// <summary>
        /// The token validated and the identity is genuine, but the application declined it — for example
        /// the user is not provisioned, or is administratively disabled.
        /// </summary>
        Rejected,

        /// <summary>
        /// The attempt failed for a reason that does not fit the other categories.
        /// </summary>
        Other,
    }
}
