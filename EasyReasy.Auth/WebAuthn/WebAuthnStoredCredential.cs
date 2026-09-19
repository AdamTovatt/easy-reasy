namespace EasyReasy.Auth
{
    /// <summary>
    /// A credential the application stored at registration, handed back so an assertion can be verified
    /// against it. The counterpart of <see cref="WebAuthnRegisteredCredential"/>: what that type reports,
    /// this type takes.
    /// </summary>
    /// <remarks>
    /// The application looks the credential up by the id the assertion names, which means it — not this
    /// library — decides whose credential this is. Verification answers whether the authenticator holding
    /// this public key signed this challenge; it cannot answer whether that credential belongs to the user
    /// who passed the first factor, because it never sees the session. Looking the credential up among
    /// <i>that user's</i> credentials is what makes the two factors belong to one person, and it is the
    /// application's step.
    /// </remarks>
    public sealed class WebAuthnStoredCredential
    {
        /// <summary>The credential id, base64url-encoded, exactly as registration reported it.</summary>
        public string CredentialId { get; }

        /// <summary>The credential's COSE public key, base64url-encoded, exactly as registration reported it.</summary>
        public string PublicKey { get; }

        /// <summary>
        /// The sign counter recorded for this credential — from registration, or from the last assertion
        /// that was verified.
        /// </summary>
        public uint SignCount { get; }

        /// <summary>The decoded credential id, for comparison against the one the assertion names.</summary>
        internal byte[] CredentialIdBytes { get; }

        /// <summary>The decoded COSE public key.</summary>
        internal byte[] PublicKeyBytes { get; }

        /// <summary>
        /// Initializes a stored credential from what was kept at registration.
        /// </summary>
        /// <param name="credentialId">The base64url credential id, as <see cref="WebAuthnRegisteredCredential.CredentialId"/> reported it.</param>
        /// <param name="publicKey">The base64url COSE public key, as <see cref="WebAuthnRegisteredCredential.PublicKey"/> reported it.</param>
        /// <param name="signCount">The sign counter last recorded for this credential.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// An argument stands for no bytes, or is not in the canonical base64url this library reported it
        /// in — either way, the value did not survive being stored.
        /// </exception>
        public WebAuthnStoredCredential(string credentialId, string publicKey, uint signCount)
        {
            ArgumentNullException.ThrowIfNull(credentialId);
            ArgumentNullException.ThrowIfNull(publicKey);

            CredentialIdBytes = DecodeStoredValue(credentialId, nameof(credentialId));
            PublicKeyBytes = DecodeStoredValue(publicKey, nameof(publicKey));

            CredentialId = credentialId;
            PublicKey = publicKey;
            SignCount = signCount;
        }

        /// <summary>
        /// Decodes a value this library handed the application and the application handed back.
        /// </summary>
        /// <remarks>
        /// Strict about the spelling, unlike the fields that arrive from a browser. These two came out of
        /// registration in canonical base64url, so anything else is a value that did not survive storage —
        /// truncated by a narrow column, re-encoded as standard base64, wrapped by a transport. Left to
        /// fail later, a mangled public key reads as a bad signature and a mangled id as the wrong
        /// credential, which are both stories about an attacker rather than about the database.
        /// <para>
        /// Deliberately not shared with the verifier's matching guard for the issued challenge. The part
        /// worth having in one place is the canonical decoding itself, and that is already
        /// <see cref="Base64UrlEncoding.TryDecodeCanonical"/>; what is left at each site is its own length
        /// rule — a credential id has no fixed length, a challenge has exactly one — and its own message. A
        /// helper taking both as parameters would be longer to call than the three lines it replaced.
        /// </para>
        /// </remarks>
        private static byte[] DecodeStoredValue(string value, string parameterName)
        {
            byte[]? decoded = Base64UrlEncoding.TryDecodeCanonical(value);

            if (decoded == null || decoded.Length == 0)
            {
                throw new ArgumentException(
                    $"'{parameterName}' is not in the canonical base64url this library reported it in, so it is not a value it produced; store what registration returned, unchanged.",
                    parameterName);
            }

            return decoded;
        }
    }
}
