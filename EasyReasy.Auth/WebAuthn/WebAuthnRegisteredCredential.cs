namespace EasyReasy.Auth
{
    /// <summary>
    /// What a verified registration produced, and what the application stores against the user. Handed back
    /// by <see cref="WebAuthnVerifier.VerifyRegistration"/>; this library keeps none of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two binary values are base64url strings rather than <c>byte[]</c>, so a credential goes into a
    /// database column and comes back out of one without the application choosing an encoding — the
    /// encoding is the library's, on both sides of the round trip, and it is the same one the browser's JSON
    /// uses. <see cref="WebAuthnStoredCredential"/> takes these two strings back in exactly this form.
    /// </para>
    /// <para>
    /// A user may hold several credentials, and the application decides how many and how they are keyed;
    /// this type describes one of them and nothing about the collection.
    /// </para>
    /// </remarks>
    public sealed class WebAuthnRegisteredCredential
    {
        /// <summary>
        /// The credential id, base64url-encoded. This is the value to put in the allow-list when
        /// generating this user's authentication options, and the key to look the credential up by when an
        /// assertion comes back naming it.
        /// </summary>
        public string CredentialId { get; }

        /// <summary>
        /// The credential's COSE public key, base64url-encoded. Opaque to the application: it is handed
        /// straight back at authentication, where the signature is verified against it.
        /// </summary>
        public string PublicKey { get; }

        /// <summary>The algorithm the public key signs with, read from the key itself.</summary>
        public CoseAlgorithm Algorithm { get; }

        /// <summary>
        /// The authenticator's signature counter at registration. Stored so the next assertion can be
        /// compared against it; frequently 0, which is what an authenticator that keeps no counter reports.
        /// </summary>
        public uint SignCount { get; }

        /// <summary>
        /// The authenticator model identifier.
        /// </summary>
        /// <remarks>
        /// Usually <see cref="Guid.Empty"/>: a client asked for attestation <c>none</c> zeroes the AAGUID
        /// when it strips the attestation statement, which is the privacy property that conveyance exists
        /// for. A real value still reaches here from an authenticator that natively emits the <c>none</c>
        /// format, having no statement to strip. Nothing in this library reads it — it is reported because
        /// it is what the authenticator said.
        /// </remarks>
        public Guid Aaguid { get; }

        /// <summary>
        /// Whether the credential may be backed up — synced to the user's other devices rather than bound
        /// to this one. Fixed for the credential's lifetime.
        /// </summary>
        /// <remarks>
        /// Worth storing even though nothing here enforces a policy on it: a credential that is not backup
        /// eligible is lost with the device, which is what makes "register a second key" advice matter, and
        /// an application can only tell the user that if it kept the flag.
        /// </remarks>
        public bool BackupEligible { get; }

        /// <summary>
        /// Whether the credential currently is backed up. Only ever set on a credential that is also
        /// <see cref="BackupEligible"/>, and unlike it this can change between ceremonies.
        /// </summary>
        public bool BackedUp { get; }

        /// <summary>
        /// How this authenticator can be reached — <c>internal</c>, <c>usb</c>, <c>nfc</c>, <c>ble</c>,
        /// <c>hybrid</c> — as the browser reported it, or null when it reported nothing.
        /// </summary>
        /// <remarks>
        /// Reported for the application to store, and read by nothing here — the same shape as
        /// <see cref="Aaguid"/>. It is carried because this is the only ceremony that produces it: an
        /// authentication response does not repeat it, so an application that does not keep it now cannot
        /// recover it later without re-enrolling the authenticator. What it is worth keeping for is the
        /// browser's own prompting — handing these values back in a later ceremony lets it ask for the
        /// right authenticator instead of offering every kind. This library's allow-list has nowhere to put
        /// them, so doing so means building that ceremony's options yourself.
        /// </remarks>
        public IReadOnlyList<string>? Transports { get; }

        internal WebAuthnRegisteredCredential(
            string credentialId,
            string publicKey,
            CoseAlgorithm algorithm,
            uint signCount,
            Guid aaguid,
            bool backupEligible,
            bool backedUp,
            IReadOnlyList<string>? transports)
        {
            CredentialId = credentialId;
            PublicKey = publicKey;
            Algorithm = algorithm;
            SignCount = signCount;
            Aaguid = aaguid;
            BackupEligible = backupEligible;
            BackedUp = backedUp;
            Transports = transports;
        }
    }
}
