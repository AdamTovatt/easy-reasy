namespace EasyReasy.Auth
{
    /// <summary>
    /// Turns what a parser or a shared check reported into the failure reason of the ceremony that was
    /// running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The translation is per ceremony rather than a shared enum, because the same failure means different
    /// things on the two paths. A COSE key that will not parse during registration is a problem with what
    /// the authenticator just produced; the same key failing during authentication is a problem with what
    /// the application stored and handed back — one is a rejected enrollment, the other a corrupted record,
    /// and an audit log that called them the same thing would send whoever reads it to the wrong place.
    /// </para>
    /// <para>
    /// This is the one place the two ceremonies diverge over shared machinery: the checks themselves live
    /// once in <see cref="WebAuthnCeremonyChecks"/>, and the parsers report one internal
    /// <see cref="WebAuthnParseError"/> set. Kept out of the verifier so a test can put every member of both
    /// enums through it and prove the mapping is total. Totality is the property that has to hold forever:
    /// a member added later must never fall through to whichever arm the switch ended in. That two members
    /// never share a reason is not such a property — it is true of some of these maps and deliberately
    /// false in others, and a member that genuinely shares a reason should be mapped to it rather than
    /// given an invented reason of its own.
    /// </para>
    /// </remarks>
    internal static class WebAuthnFailureTranslation
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

        /// <summary>
        /// The authentication failure reason a parse error reads as.
        /// </summary>
        /// <param name="error">What the structure failed to be.</param>
        /// <returns>The reason to report.</returns>
        /// <exception cref="InvalidOperationException">The error has no mapping, which the exhaustiveness test prevents.</exception>
        public static WebAuthnAuthenticationFailureReason ToAuthenticationReason(WebAuthnParseError error)
        {
            return error switch
            {
                WebAuthnParseError.MalformedClientData => WebAuthnAuthenticationFailureReason.MalformedClientData,
                WebAuthnParseError.MalformedAuthenticatorData => WebAuthnAuthenticationFailureReason.MalformedAuthenticatorData,

                // Both describe the stored credential here, where the key came out of the application's own
                // store rather than from an authenticator — so unlike at registration they are one reason.
                // Do not split them back apart to make the map injective: injectivity is not a property of
                // this map, and the test that pins the sharing says so.
                WebAuthnParseError.MalformedPublicKey => WebAuthnAuthenticationFailureReason.MalformedStoredCredential,
                WebAuthnParseError.UnsupportedAlgorithm => WebAuthnAuthenticationFailureReason.MalformedStoredCredential,

                // An assertion carries no attestation object, so nothing on this path parses one and neither
                // error can be raised here. They are mapped rather than left to the throwing arm to keep
                // totality a property a test can check by itself: the alternative is a hand-maintained list
                // of which errors each path can reach, and a mistake in that list turns a declined ceremony
                // into an escaped exception. An assertion that did produce one would mean this verifier had
                // been changed to parse attestation, and the mapping would have to be revisited with it.
                WebAuthnParseError.MalformedAttestationObject => WebAuthnAuthenticationFailureReason.MalformedAuthenticatorData,
                WebAuthnParseError.UnsupportedAttestationFormat => WebAuthnAuthenticationFailureReason.MalformedAuthenticatorData,

                _ => throw new InvalidOperationException($"No authentication failure reason is mapped for {nameof(WebAuthnParseError)}.{error}."),
            };
        }

        /// <summary>
        /// The registration failure reason a shared ceremony check reads as.
        /// </summary>
        /// <param name="check">The check that rejected the ceremony.</param>
        /// <returns>The reason to report.</returns>
        /// <exception cref="InvalidOperationException">The check has no mapping, which the exhaustiveness test prevents.</exception>
        public static WebAuthnRegistrationFailureReason ToRegistrationReason(WebAuthnCeremonyCheck check)
        {
            return check switch
            {
                WebAuthnCeremonyCheck.WrongCeremonyType => WebAuthnRegistrationFailureReason.WrongCeremonyType,
                WebAuthnCeremonyCheck.ChallengeMismatch => WebAuthnRegistrationFailureReason.ChallengeMismatch,
                WebAuthnCeremonyCheck.OriginNotAllowed => WebAuthnRegistrationFailureReason.OriginNotAllowed,
                WebAuthnCeremonyCheck.CrossOriginCeremony => WebAuthnRegistrationFailureReason.CrossOriginCeremony,
                WebAuthnCeremonyCheck.RelyingPartyIdHashMismatch => WebAuthnRegistrationFailureReason.RelyingPartyIdHashMismatch,
                WebAuthnCeremonyCheck.UserNotPresent => WebAuthnRegistrationFailureReason.UserNotPresent,
                WebAuthnCeremonyCheck.UserNotVerified => WebAuthnRegistrationFailureReason.UserNotVerified,

                _ => throw new InvalidOperationException($"No registration failure reason is mapped for {nameof(WebAuthnCeremonyCheck)}.{check}."),
            };
        }

        /// <summary>
        /// The authentication failure reason a shared ceremony check reads as.
        /// </summary>
        /// <param name="check">The check that rejected the ceremony.</param>
        /// <returns>The reason to report.</returns>
        /// <exception cref="InvalidOperationException">The check has no mapping, which the exhaustiveness test prevents.</exception>
        public static WebAuthnAuthenticationFailureReason ToAuthenticationReason(WebAuthnCeremonyCheck check)
        {
            return check switch
            {
                WebAuthnCeremonyCheck.WrongCeremonyType => WebAuthnAuthenticationFailureReason.WrongCeremonyType,
                WebAuthnCeremonyCheck.ChallengeMismatch => WebAuthnAuthenticationFailureReason.ChallengeMismatch,
                WebAuthnCeremonyCheck.OriginNotAllowed => WebAuthnAuthenticationFailureReason.OriginNotAllowed,
                WebAuthnCeremonyCheck.CrossOriginCeremony => WebAuthnAuthenticationFailureReason.CrossOriginCeremony,
                WebAuthnCeremonyCheck.RelyingPartyIdHashMismatch => WebAuthnAuthenticationFailureReason.RelyingPartyIdHashMismatch,
                WebAuthnCeremonyCheck.UserNotPresent => WebAuthnAuthenticationFailureReason.UserNotPresent,
                WebAuthnCeremonyCheck.UserNotVerified => WebAuthnAuthenticationFailureReason.UserNotVerified,

                _ => throw new InvalidOperationException($"No authentication failure reason is mapped for {nameof(WebAuthnCeremonyCheck)}.{check}."),
            };
        }
    }
}
