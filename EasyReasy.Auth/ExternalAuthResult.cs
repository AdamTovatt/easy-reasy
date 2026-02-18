namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the result of an external identity-provider authentication attempt
    /// (for example a Google, Apple, or Microsoft sign-in). Use the static factory methods
    /// <see cref="Succeeded"/> and <see cref="Failed"/> to create instances.
    /// </summary>
    /// <remarks>
    /// Deliberately does not carry the raw provider token, so that consumer code logging the whole result
    /// cannot accidentally leak a credential. On the success path the embedded <see cref="AuthResponse"/>
    /// still carries the issued JWT and (when refresh tokens are enabled) the raw refresh token — see the
    /// remarks on <see cref="IAuthAuditLogger"/> for safe-logging guidance.
    /// </remarks>
    public sealed class ExternalAuthResult
    {
        /// <summary>
        /// The identity provider that handled the attempt, in lower case (for example <c>"google"</c>).
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// Whether the authentication attempt succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The authentication response containing the access token and (optionally) a refresh token.
        /// Only populated when <see cref="Success"/> is true.
        /// </summary>
        public AuthResponse? AuthResponse { get; }

        /// <summary>
        /// The subject identifier associated with the attempt — the provider's stable user id — when known.
        /// Populated on success, and on failure when the identity validated but was then rejected. Null when
        /// the provider token itself failed to validate, because no trustworthy subject is available.
        /// </summary>
        public string? AttemptedSubject { get; }

        /// <summary>
        /// The reason the attempt failed.
        /// Only populated when <see cref="Success"/> is false.
        /// </summary>
        public ExternalAuthFailureReason? FailureReason { get; }

        private ExternalAuthResult(
            string provider,
            bool success,
            AuthResponse? authResponse,
            string? attemptedSubject,
            ExternalAuthFailureReason? failureReason)
        {
            Provider = provider;
            Success = success;
            AuthResponse = authResponse;
            AttemptedSubject = attemptedSubject;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Creates a successful external authentication result.
        /// </summary>
        /// <param name="provider">The identity provider that authenticated the user (for example <c>"google"</c>).</param>
        /// <param name="authResponse">The authentication response containing the access token.</param>
        /// <param name="subject">The subject identifier (the provider's stable user id) that was authenticated.</param>
        /// <returns>A successful <see cref="ExternalAuthResult"/>.</returns>
        public static ExternalAuthResult Succeeded(string provider, AuthResponse authResponse, string subject)
        {
            return new ExternalAuthResult(provider, true, authResponse, subject, null);
        }

        /// <summary>
        /// Creates a failed external authentication result.
        /// </summary>
        /// <param name="provider">The identity provider that handled the attempt (for example <c>"google"</c>).</param>
        /// <param name="reason">The reason the attempt failed.</param>
        /// <param name="attemptedSubject">
        /// The subject identifier that was attempted, when available. Populate it when the provider token
        /// validated but the identity was then rejected; leave it <c>null</c> when the token itself failed
        /// to validate and no trustworthy subject exists.
        /// </param>
        /// <returns>A failed <see cref="ExternalAuthResult"/>.</returns>
        public static ExternalAuthResult Failed(string provider, ExternalAuthFailureReason reason, string? attemptedSubject = null)
        {
            return new ExternalAuthResult(provider, false, null, attemptedSubject, reason);
        }
    }
}
