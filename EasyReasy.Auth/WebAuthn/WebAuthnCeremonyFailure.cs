namespace EasyReasy.Auth
{
    /// <summary>
    /// The check that rejected a ceremony, and what to say about it.
    /// </summary>
    /// <remarks>
    /// Carries the message rather than leaving each ceremony to compose its own, because the checks are
    /// shared and so are the facts worth stating about them: which origin ran the ceremony, which relying
    /// party the authenticator answered for. The ceremony the message belongs to does not need naming in
    /// it — the result it lands on is already a registration's or an authentication's.
    /// </remarks>
    internal sealed class WebAuthnCeremonyFailure
    {
        /// <summary>The check that failed.</summary>
        public WebAuthnCeremonyCheck Check { get; }

        /// <summary>A description of the specific problem, for logs.</summary>
        public string Message { get; }

        /// <summary>
        /// Records a failed check.
        /// </summary>
        /// <param name="check">The check that failed.</param>
        /// <param name="message">A description of the specific problem, for logs.</param>
        public WebAuthnCeremonyFailure(WebAuthnCeremonyCheck check, string message)
        {
            Check = check;
            Message = message;
        }
    }
}
