namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the reason a username/password login attempt failed.
    /// Intended to flow into audit logs (e.g. ISO 27001 A.12.4.1 records of failed authentication attempts)
    /// — never returned to the client.
    /// </summary>
    public enum LoginFailureReason
    {
        /// <summary>
        /// No user exists with the supplied identifier.
        /// </summary>
        UnknownUser,

        /// <summary>
        /// The user exists but the supplied credentials did not validate.
        /// </summary>
        InvalidCredentials,

        /// <summary>
        /// The user exists but is disabled (administratively deactivated).
        /// </summary>
        UserDisabled,

        /// <summary>
        /// The user exists but is temporarily locked (e.g. repeated failed attempts).
        /// </summary>
        UserLocked,

        /// <summary>
        /// The login failed for a consumer-specific reason that does not fit the other categories.
        /// </summary>
        Other,
    }
}
