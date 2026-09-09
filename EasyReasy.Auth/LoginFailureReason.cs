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

        /// <summary>
        /// The request did not carry the credentials needed to make an authentication attempt —
        /// the username or the password was absent or empty. The credential store was never consulted.
        /// Emitted by the built-in login endpoint before <see cref="IAuthRequestValidationService"/> is called;
        /// the caller receives a <c>400 Bad Request</c> rather than a <c>401 Unauthorized</c>.
        /// </summary>
        /// <remarks>
        /// This member does not represent an authentication attempt: nothing was tried. A consumer counting
        /// failed attempts per account — for lockout, or for an A.12.4.1 failure rate — should exclude it, or
        /// anyone could lock any account they can name without ever guessing a credential.
        /// </remarks>
        MissingCredentials,
    }
}
