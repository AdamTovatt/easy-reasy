namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of verifying a WebAuthn authentication ceremony. Like
    /// <see cref="WebAuthnRegistrationResult"/> and <see cref="LoginResult"/>, a declined ceremony is a
    /// result carrying a reason rather than an exception.
    /// </summary>
    public sealed class WebAuthnAuthenticationResult
    {
        /// <summary>Whether the assertion was accepted.</summary>
        public bool Success { get; }

        /// <summary>
        /// The sign counter to store against the credential. Only meaningful when <see cref="Success"/> is
        /// true.
        /// </summary>
        /// <remarks>
        /// Write it back even when <see cref="SignCounterState"/> is
        /// <see cref="WebAuthnSignCounterState.NotSupported"/>: it is 0 there, storing 0 over 0 changes
        /// nothing, and having one unconditional write is what stops an authenticator that starts counting
        /// — after a firmware update, or because the user moved to a different one — from being compared
        /// against a value that stopped being updated.
        /// </remarks>
        public uint SignCount { get; }

        /// <summary>
        /// Whether the counter advanced or the authenticator keeps none. Null when <see cref="Success"/> is
        /// false.
        /// </summary>
        /// <remarks>
        /// Nullable so that a declined ceremony cannot claim a verdict it never reached. Both states are
        /// affirmative findings about a <i>verified</i> assertion, and neither can stand in for "no counter
        /// was compared" — least of all
        /// <see cref="WebAuthnAuthenticationFailureReason.SignCounterRegressed"/>, which is the counter
        /// check having fired, the opposite of
        /// <see cref="WebAuthnSignCounterState.NotSupported"/>'s "there was no counter check".
        /// </remarks>
        public WebAuthnSignCounterState? SignCounterState { get; }

        /// <summary>
        /// Whether the authenticator reported that it verified the user during this ceremony — a biometric
        /// or a PIN, rather than a touch. Only meaningful when <see cref="Success"/> is true.
        /// </summary>
        public bool UserVerified { get; }

        /// <summary>
        /// Whether the credential is currently backed up — synced to the user's other devices. Only
        /// meaningful when <see cref="Success"/> is true.
        /// </summary>
        /// <remarks>
        /// Reported on every assertion because, unlike backup eligibility, it can change over a
        /// credential's life: a user turning on a cloud keychain moves their credential from one device to
        /// several. An application that recorded it at registration should update it from here.
        /// </remarks>
        public bool BackedUp { get; }

        /// <summary>Why the assertion was declined. Only populated when <see cref="Success"/> is false.</summary>
        public WebAuthnAuthenticationFailureReason? FailureReason { get; }

        /// <summary>
        /// A description of the specific problem, for logs. Only populated when <see cref="Success"/> is
        /// false. Diagnostic rather than a message to show a user; see
        /// <see cref="WebAuthnRegistrationResult.FailureMessage"/>.
        /// </summary>
        public string? FailureMessage { get; }

        /// <summary>
        /// Initializes an accepted assertion. Separate from the declined constructor rather than one taking
        /// both halves: a single constructor would have the failure path passing values for four fields it
        /// has nothing to say about, two of them adjacent booleans a transposition would not show up in.
        /// </summary>
        private WebAuthnAuthenticationResult(
            uint signCount,
            WebAuthnSignCounterState signCounterState,
            bool userVerified,
            bool backedUp)
        {
            Success = true;
            SignCount = signCount;
            SignCounterState = signCounterState;
            UserVerified = userVerified;
            BackedUp = backedUp;
        }

        /// <summary>
        /// Initializes a declined assertion, leaving everything the ceremony never established unset.
        /// </summary>
        private WebAuthnAuthenticationResult(WebAuthnAuthenticationFailureReason failureReason, string failureMessage)
        {
            Success = false;
            FailureReason = failureReason;
            FailureMessage = failureMessage;
        }

        /// <summary>
        /// Creates the result of an accepted assertion.
        /// </summary>
        /// <param name="signCount">The counter to store against the credential.</param>
        /// <param name="signCounterState">Whether the counter advanced or the authenticator keeps none.</param>
        /// <param name="userVerified">Whether the authenticator verified the user.</param>
        /// <param name="backedUp">Whether the credential is currently backed up.</param>
        /// <returns>A successful result.</returns>
        internal static WebAuthnAuthenticationResult Succeeded(
            uint signCount,
            WebAuthnSignCounterState signCounterState,
            bool userVerified,
            bool backedUp)
        {
            return new WebAuthnAuthenticationResult(signCount, signCounterState, userVerified, backedUp);
        }

        /// <summary>
        /// Creates the result of a declined assertion.
        /// </summary>
        /// <param name="failureReason">Which check failed.</param>
        /// <param name="failureMessage">A description of the specific problem, for logs.</param>
        /// <returns>A failed result.</returns>
        internal static WebAuthnAuthenticationResult Failed(WebAuthnAuthenticationFailureReason failureReason, string failureMessage)
        {
            return new WebAuthnAuthenticationResult(failureReason, failureMessage);
        }
    }
}
