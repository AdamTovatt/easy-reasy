namespace EasyReasy.Auth
{
    /// <summary>
    /// A stored credential handed back to <see cref="WebAuthnOptionsGenerator"/>: its id, and the transports
    /// its registration reported. The same input names a credential to offer at authentication and one to
    /// refuse at registration, so a stored credential goes back the same way in either ceremony.
    /// </summary>
    /// <remarks>
    /// Both values are taken exactly as <see cref="WebAuthnRegisteredCredential"/> reported them, which is
    /// the form the application stored. Only a null id is refused here, since a reference with no id is no
    /// reference at all and is best refused where it is written. Every other check is the generator's, so the
    /// failure names the argument the application passed it: the same rules a plain credential id gets
    /// there, and the same bounds registration put on the transports.
    /// </remarks>
    public sealed class WebAuthnCredentialReference
    {
        /// <summary>
        /// The credential id, base64url-encoded — <see cref="WebAuthnRegisteredCredential.CredentialId"/> as
        /// stored.
        /// </summary>
        public string CredentialId { get; }

        /// <summary>
        /// The transports registration reported — <see cref="WebAuthnRegisteredCredential.Transports"/> as
        /// stored — or null when none were kept.
        /// </summary>
        /// <remarks>
        /// Written as the descriptor's <c>transports</c>, which lets the browser go straight to the
        /// authenticator they name rather than offering every kind. Null and an empty list both leave the
        /// field out, since an empty list gives the browser nothing to go on.
        /// </remarks>
        public IReadOnlyList<string>? Transports { get; }

        /// <summary>
        /// Initializes the reference from what the application stored for one credential.
        /// </summary>
        /// <param name="credentialId">The stored credential id, base64url.</param>
        /// <param name="transports">The stored transports, or null when none were kept.</param>
        /// <exception cref="ArgumentNullException"><paramref name="credentialId"/> is null.</exception>
        public WebAuthnCredentialReference(string credentialId, IReadOnlyList<string>? transports = null)
        {
            ArgumentNullException.ThrowIfNull(credentialId);

            CredentialId = credentialId;
            Transports = transports;
        }
    }
}
