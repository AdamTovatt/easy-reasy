namespace EasyReasy.Auth
{
    /// <summary>
    /// Why a WebAuthn authentication ceremony was not accepted. Every check verification makes has its own
    /// member, so an audit log records which one failed rather than that one did —
    /// <see cref="IAuthAuditLogger.OnWebAuthnAuthenticationAsync"/> is the hook that receives it.
    /// </summary>
    public enum WebAuthnAuthenticationFailureReason
    {
        /// <summary>The collected client data was not a JSON object carrying the fields WebAuthn defines.</summary>
        MalformedClientData,

        /// <summary>The authenticator data was truncated or self-inconsistent.</summary>
        MalformedAuthenticatorData,

        /// <summary>
        /// The stored credential could not be read: its public key is not a well-formed COSE key, or it
        /// states an algorithm this library does not verify.
        /// </summary>
        /// <remarks>
        /// One reason for both, because on this path they mean the same thing. The key was not produced by
        /// the authenticator just now — it came out of the application's own store, having been reported by
        /// a registration this library verified — so either failure says the stored value is not what was
        /// handed over. At registration the two are distinct, because there they describe what an
        /// authenticator just offered.
        /// </remarks>
        MalformedStoredCredential,

        /// <summary>
        /// The client data records a different ceremony than <c>webauthn.get</c> — a registration response
        /// presented as an authentication.
        /// </summary>
        WrongCeremonyType,

        /// <summary>
        /// The challenge in the client data is not the one that was issued. A stale assertion, a replayed
        /// one, or one answering a challenge from a different session.
        /// </summary>
        ChallengeMismatch,

        /// <summary>The page that ran the ceremony was not at one of the relying party's configured origins.</summary>
        OriginNotAllowed,

        /// <summary>
        /// The ceremony ran inside a frame whose ancestors are not all same-origin with it — the relying
        /// party's page was embedded by another site when the user was prompted.
        /// </summary>
        CrossOriginCeremony,

        /// <summary>
        /// The authenticator data is scoped to a different relying-party id than the one configured.
        /// </summary>
        RelyingPartyIdHashMismatch,

        /// <summary>
        /// The authenticator did not report user presence. WebAuthn requires it on every assertion, so this
        /// is an assertion produced without anybody touching the authenticator.
        /// </summary>
        UserNotPresent,

        /// <summary>
        /// The authenticator did not report that it verified the user, and authentication was requested
        /// with <see cref="WebAuthnUserVerificationRequirement.Required"/>.
        /// </summary>
        UserNotVerified,

        /// <summary>
        /// The assertion names a different credential than the one it was verified against — the
        /// application looked up a credential that is not the one the authenticator answered with.
        /// </summary>
        CredentialIdMismatch,

        /// <summary>
        /// The signature does not verify against the stored public key. The authenticator holding the
        /// private key did not produce this assertion, or it did not sign these bytes.
        /// </summary>
        SignatureInvalid,

        /// <summary>
        /// The authenticator's signature counter did not advance, and both it and the stored counter were
        /// not zero. WebAuthn names this as the signal of a cloned credential: two copies of one private
        /// key, each keeping its own count.
        /// </summary>
        /// <remarks>
        /// Deliberately distinct from an authenticator that keeps no counter at all, which is reported as
        /// <see cref="WebAuthnSignCounterState.NotSupported"/> on a ceremony that <i>succeeded</i>. The two
        /// look alike in the data — a counter that did not go up — and mean opposite things, so collapsing
        /// them would either reject every platform authenticator or silence the one signal this check
        /// exists to raise. What to do about a regression is the application's policy: it is evidence, not
        /// proof, and a counter can also regress because an authenticator was restored from a backup.
        /// </remarks>
        SignCounterRegressed,
    }
}
