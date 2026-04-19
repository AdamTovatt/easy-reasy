namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the result of a username/password login validation.
    /// Use the static factory methods <see cref="Succeeded"/> and <see cref="Failed"/> to create instances.
    /// </summary>
    /// <remarks>
    /// Deliberately does not carry the raw password or any other credential, so that consumer code
    /// logging the whole result cannot accidentally leak secrets.
    /// </remarks>
    public sealed class LoginResult
    {
        /// <summary>
        /// Whether the login attempt succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The authentication response containing the access token and (optionally) a refresh token.
        /// Only populated when <see cref="Success"/> is true.
        /// </summary>
        public AuthResponse? AuthResponse { get; }

        /// <summary>
        /// The subject identifier (user id or username) associated with the attempt.
        /// Populated on success, and on failure when the attempted identifier is known — even if the user does not exist
        /// (<see cref="LoginFailureReason.UnknownUser"/>). Intended to support ISO 27001 A.12.4.1 audit logging.
        /// </summary>
        public string? AttemptedSubject { get; }

        /// <summary>
        /// The reason the login attempt failed.
        /// Only populated when <see cref="Success"/> is false.
        /// </summary>
        public LoginFailureReason? FailureReason { get; }

        private LoginResult(
            bool success,
            AuthResponse? authResponse,
            string? attemptedSubject,
            LoginFailureReason? failureReason)
        {
            Success = success;
            AuthResponse = authResponse;
            AttemptedSubject = attemptedSubject;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Creates a successful login result.
        /// </summary>
        /// <param name="authResponse">The authentication response containing the access token.</param>
        /// <param name="subject">The subject identifier (user id) that was authenticated.</param>
        /// <returns>A successful <see cref="LoginResult"/>.</returns>
        public static LoginResult Succeeded(AuthResponse authResponse, string subject)
        {
            return new LoginResult(true, authResponse, subject, null);
        }

        /// <summary>
        /// Creates a failed login result.
        /// </summary>
        /// <param name="reason">The reason the login attempt failed.</param>
        /// <param name="attemptedSubject">
        /// The subject identifier that was attempted, when available. May be <c>null</c> if the request did not
        /// carry a parseable identifier. Populate whenever possible so audit logs can attribute the failure.
        /// </param>
        /// <returns>A failed <see cref="LoginResult"/>.</returns>
        public static LoginResult Failed(LoginFailureReason reason, string? attemptedSubject = null)
        {
            return new LoginResult(false, null, attemptedSubject, reason);
        }
    }
}
