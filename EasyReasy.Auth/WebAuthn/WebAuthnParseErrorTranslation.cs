namespace EasyReasy.Auth
{
    /// <summary>
    /// Turns what a parser reported into the failure reason of the ceremony that was running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The translation is per ceremony rather than a shared enum, because the same structural failure means
    /// different things on the two paths. A COSE key that will not parse during registration is a problem
    /// with what the authenticator just produced; the same key failing during authentication is a problem
    /// with what the application stored and handed back — one is a rejected enrollment, the other a
    /// corrupted record, and an audit log that called them the same thing would send whoever reads it to
    /// the wrong place.
    /// </para>
    /// <para>
    /// Kept out of the verifier so a test can put every <see cref="WebAuthnParseError"/> through it and
    /// prove the mapping is total. Totality is the property that has to hold forever: an error added later
    /// must never fall through to whichever arm the switch ended in. That two errors never share a reason
    /// is not such a property — it is true of the errors that exist now, and an error that genuinely shares
    /// one should be mapped to it rather than given an invented reason of its own.
    /// </para>
    /// </remarks>
    internal static class WebAuthnParseErrorTranslation
    {
        /// <summary>
        /// The registration failure reason a parse error reads as.
        /// </summary>
        /// <param name="error">What the structure failed to be.</param>
        /// <returns>The reason to report.</returns>
        /// <exception cref="InvalidOperationException">The error has no mapping, which the exhaustiveness test prevents.</exception>
        public static WebAuthnRegistrationFailureReason ToRegistrationReason(WebAuthnParseError error)
        {
            return error switch
            {
                WebAuthnParseError.MalformedClientData => WebAuthnRegistrationFailureReason.MalformedClientData,
                WebAuthnParseError.MalformedAttestationObject => WebAuthnRegistrationFailureReason.MalformedAttestationObject,
                WebAuthnParseError.UnsupportedAttestationFormat => WebAuthnRegistrationFailureReason.UnsupportedAttestationFormat,
                WebAuthnParseError.MalformedAuthenticatorData => WebAuthnRegistrationFailureReason.MalformedAuthenticatorData,
                WebAuthnParseError.MalformedPublicKey => WebAuthnRegistrationFailureReason.MalformedCredentialPublicKey,
                WebAuthnParseError.UnsupportedAlgorithm => WebAuthnRegistrationFailureReason.UnsupportedAlgorithm,

                // Folding an unmapped error into a neighbouring reason would put a reason naming the wrong
                // check into an audit log, which is worse than a failure naming none.
                _ => throw new InvalidOperationException($"No registration failure reason is mapped for {nameof(WebAuthnParseError)}.{error}."),
            };
        }
    }
}
