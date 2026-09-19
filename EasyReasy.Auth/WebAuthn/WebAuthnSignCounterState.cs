namespace EasyReasy.Auth
{
    /// <summary>
    /// What the signature counter said on a verified assertion. A declined one has no member here —
    /// <see cref="WebAuthnAuthenticationResult.SignCounterState"/> is null.
    /// </summary>
    /// <remarks>
    /// The counter is WebAuthn's clone detector: an authenticator that keeps one increments it on every
    /// assertion, so a count that fails to advance means two copies of a private key that should exist
    /// once. Plenty of authenticators keep no counter at all, though — Touch ID and Face ID among them,
    /// which report 0 forever — and their assertions are perfectly valid. This says which of the two
    /// happened, so an application can tell "the clone check passed" from "there was no clone check".
    /// </remarks>
    public enum WebAuthnSignCounterState
    {
        /// <summary>
        /// The authenticator keeps no counter: it reported 0, and 0 is what was stored. WebAuthn Level 3
        /// §7.2 skips the comparison in exactly this case, and it is the common one — a strict
        /// requirement that the count advance would reject every assertion from a platform authenticator,
        /// which is the factor most of this feature's users will have.
        /// </summary>
        NotSupported,

        /// <summary>
        /// The counter advanced past the stored value, as an authenticator that keeps one does on every
        /// assertion. Store the new value from <see cref="WebAuthnAuthenticationResult.SignCount"/> so the
        /// next assertion is compared against it — a counter that is never written back is a check that
        /// only ever compares against registration.
        /// </summary>
        Advanced,
    }
}
