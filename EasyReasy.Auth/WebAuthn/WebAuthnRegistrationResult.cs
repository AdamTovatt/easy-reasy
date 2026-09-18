namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of verifying a WebAuthn registration ceremony. Following the
    /// <see cref="LoginResult"/> precedent: a declined ceremony is a result carrying a reason, not an
    /// exception, because a security key that failed a check is an outcome of authentication rather than an
    /// error in it.
    /// </summary>
    public sealed class WebAuthnRegistrationResult
    {
        /// <summary>Whether the registration was accepted.</summary>
        public bool Success { get; }

        /// <summary>
        /// What to store against the user. Only populated when <see cref="Success"/> is true.
        /// </summary>
        public WebAuthnRegisteredCredential? Credential { get; }

        /// <summary>
        /// Whether the authenticator reported that it verified the user during this ceremony — that a
        /// biometric or PIN was presented, not merely that something was touched. Only meaningful when
        /// <see cref="Success"/> is true.
        /// </summary>
        /// <remarks>
        /// Always true on a ceremony requested with
        /// <see cref="WebAuthnUserVerificationRequirement.Required"/>, since verification enforces it there.
        /// Under the other two settings it is what actually happened, which is what an application needs to
        /// record if it wants to know later whether this credential was ever enrolled with verification.
        /// </remarks>
        public bool UserVerified { get; }

        /// <summary>
        /// Why the registration was declined. Only populated when <see cref="Success"/> is false.
        /// </summary>
        public WebAuthnRegistrationFailureReason? FailureReason { get; }

        /// <summary>
        /// A description of the specific problem, for logs. Only populated when <see cref="Success"/> is
        /// false.
        /// </summary>
        /// <remarks>
        /// Diagnostic, not a message to show a user: it names structures and offsets, and a failing
        /// ceremony should tell the user no more than that their security key was not accepted.
        /// <see cref="FailureReason"/> is the value to branch on and to record — this one exists because a
        /// malformed CBOR structure is otherwise undiagnosable from an enum alone.
        /// </remarks>
        public string? FailureMessage { get; }

        private WebAuthnRegistrationResult(
            bool success,
            WebAuthnRegisteredCredential? credential,
            bool userVerified,
            WebAuthnRegistrationFailureReason? failureReason,
            string? failureMessage)
        {
            Success = success;
            Credential = credential;
            UserVerified = userVerified;
            FailureReason = failureReason;
            FailureMessage = failureMessage;
        }

        /// <summary>
        /// Creates the result of an accepted registration.
        /// </summary>
        /// <param name="credential">What to store against the user.</param>
        /// <param name="userVerified">Whether the authenticator verified the user.</param>
        /// <returns>A successful result.</returns>
        internal static WebAuthnRegistrationResult Succeeded(WebAuthnRegisteredCredential credential, bool userVerified)
        {
            return new WebAuthnRegistrationResult(true, credential, userVerified, null, null);
        }

        /// <summary>
        /// Creates the result of a declined registration.
        /// </summary>
        /// <param name="failureReason">Which check failed.</param>
        /// <param name="failureMessage">A description of the specific problem, for logs.</param>
        /// <returns>A failed result.</returns>
        internal static WebAuthnRegistrationResult Failed(WebAuthnRegistrationFailureReason failureReason, string failureMessage)
        {
            return new WebAuthnRegistrationResult(false, null, false, failureReason, failureMessage);
        }
    }
}
