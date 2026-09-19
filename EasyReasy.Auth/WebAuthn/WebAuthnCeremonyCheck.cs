namespace EasyReasy.Auth
{
    /// <summary>
    /// A check both WebAuthn ceremonies make, named so that each can report it as a member of its own
    /// public failure-reason enum.
    /// </summary>
    /// <remarks>
    /// Registration and authentication run these in the same order over the same structures, so they are
    /// written once in <see cref="WebAuthnCeremonyChecks"/> rather than twice: two copies of a check
    /// sequence in a security library is a place for one path to be fixed and the other left behind.
    /// What genuinely differs between the ceremonies is which reason an application is told, and
    /// <see cref="WebAuthnFailureTranslation"/> is where that difference lives.
    /// </remarks>
    internal enum WebAuthnCeremonyCheck
    {
        /// <summary>The client data records a different ceremony than the one being verified.</summary>
        WrongCeremonyType,

        /// <summary>The client data answers a different challenge than the one issued.</summary>
        ChallengeMismatch,

        /// <summary>The ceremony ran at an origin outside the relying party's configured set.</summary>
        OriginNotAllowed,

        /// <summary>The ceremony ran inside a frame embedded by another site.</summary>
        CrossOriginCeremony,

        /// <summary>The authenticator data is scoped to a different relying-party id.</summary>
        RelyingPartyIdHashMismatch,

        /// <summary>The authenticator did not report user presence.</summary>
        UserNotPresent,

        /// <summary>The authenticator did not report user verification, which was required.</summary>
        UserNotVerified,
    }
}
