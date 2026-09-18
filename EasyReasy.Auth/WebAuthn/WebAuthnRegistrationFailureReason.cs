namespace EasyReasy.Auth
{
    /// <summary>
    /// Why a WebAuthn registration ceremony was not accepted. Every check verification makes has its own
    /// member, so an audit log records which one failed rather than that one did.
    /// </summary>
    /// <remarks>
    /// The distinctions are the point. A ceremony rejected for a challenge that does not match is a stale
    /// or replayed registration; one rejected for an origin is a page running somewhere it should not be;
    /// one rejected for a cleared user-verification flag is a policy the authenticator could not meet. They
    /// call for different responses, and a boolean would make them the same event.
    /// </remarks>
    public enum WebAuthnRegistrationFailureReason
    {
        /// <summary>The collected client data was not a JSON object carrying the fields WebAuthn defines.</summary>
        MalformedClientData,

        /// <summary>The attestation object was not CBOR of the expected shape.</summary>
        MalformedAttestationObject,

        /// <summary>
        /// The attestation statement uses a format this library does not verify; only <c>none</c> is.
        /// </summary>
        /// <remarks>
        /// Not something an application can cause: the options this library generates always request
        /// attestation <c>none</c>, so reaching this means an authenticator answered with a conveyance
        /// nobody asked for.
        /// </remarks>
        UnsupportedAttestationFormat,

        /// <summary>The authenticator data inside the attestation object was truncated or self-inconsistent.</summary>
        MalformedAuthenticatorData,

        /// <summary>The credential's COSE public key was not a well-formed key of its stated type.</summary>
        MalformedCredentialPublicKey,

        /// <summary>The credential's public key states an algorithm outside <see cref="CoseAlgorithm"/>.</summary>
        UnsupportedAlgorithm,

        /// <summary>
        /// The client data records a different ceremony than <c>webauthn.create</c> — an authentication
        /// response presented as a registration.
        /// </summary>
        WrongCeremonyType,

        /// <summary>
        /// The challenge in the client data is not the one that was issued. A stale registration, a replayed
        /// one, or one answering a challenge from a different session.
        /// </summary>
        ChallengeMismatch,

        /// <summary>The page that ran the ceremony was not at one of the relying party's configured origins.</summary>
        OriginNotAllowed,

        /// <summary>
        /// The ceremony ran inside a frame whose ancestors are not all same-origin with it — the relying
        /// party's page was embedded by another site when the user was prompted.
        /// </summary>
        /// <remarks>
        /// Declined outright rather than judged. Deciding whose frame is acceptable needs a policy naming
        /// the embedders, and a second factor has none; enrolling one through somebody else's page is not
        /// something this library completes.
        /// </remarks>
        CrossOriginCeremony,

        /// <summary>
        /// The authenticator data is scoped to a different relying-party id than the one configured. The
        /// credential would belong to another deployment.
        /// </summary>
        RelyingPartyIdHashMismatch,

        /// <summary>
        /// The authenticator did not report user presence. WebAuthn requires it on every registration, so
        /// this is a credential created without anybody touching the authenticator.
        /// </summary>
        UserNotPresent,

        /// <summary>
        /// The authenticator did not report that it verified the user, and registration was requested with
        /// <see cref="WebAuthnUserVerificationRequirement.Required"/>.
        /// </summary>
        UserNotVerified,

        /// <summary>
        /// The authenticator data carried no attested credential data, so there is no credential id and no
        /// public key to store. A registration ceremony has to produce both.
        /// </summary>
        MissingAttestedCredentialData,

        /// <summary>
        /// The credential id in the response's <c>id</c> is not the one inside the authenticator data. The
        /// two name the same credential in every real ceremony, and storing either of a disagreeing pair
        /// would produce a credential that can never be found again.
        /// </summary>
        CredentialIdMismatch,
    }
}
