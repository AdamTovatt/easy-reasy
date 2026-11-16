namespace EasyReasy.Auth
{
    /// <summary>
    /// Which kind of WebAuthn authentication a result came from. Set by <see cref="WebAuthnVerifier"/>
    /// from the method that was called, never by the caller, so an audit record can tell a failed second
    /// factor from an unauthenticated passkey login attempt.
    /// </summary>
    public enum WebAuthnAuthenticationMode
    {
        /// <summary>
        /// <see cref="WebAuthnVerifier.VerifyAuthentication"/>: a second factor for a user a first factor
        /// already named. A failure is an attempt to finish a login that already passed a first factor.
        /// </summary>
        SecondFactor,

        /// <summary>
        /// <see cref="WebAuthnVerifier.VerifyDiscoverableAuthentication"/>: usernameless passkey sign-in,
        /// the first factor itself. A failure is a login attempt with no known user.
        /// </summary>
        Discoverable,
    }
}
