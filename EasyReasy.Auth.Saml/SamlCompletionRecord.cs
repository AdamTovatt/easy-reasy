namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The outcome of a response the assertion consumer service checked, as <see cref="ISamlAuthStore"/> keeps it until
    /// the completion page redeems it: found by the hash of the single-use code the browser was redirected with.
    /// </summary>
    /// <remarks>
    /// The store holds the record as given and never reads inside it: <see cref="SealedOutcome"/> is encrypted and
    /// authenticated with <see cref="SamlAuthOptions.StateKey"/>, so the store holds no identity in plaintext, and a row
    /// written by anything but the package is refused.
    /// </remarks>
    public sealed class SamlCompletionRecord
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamlCompletionRecord"/> class.
        /// </summary>
        /// <param name="codeHash">The SHA-256 hash of the completion code, base64url-encoded.</param>
        /// <param name="sealedOutcome">The sealed outcome.</param>
        /// <param name="expiresAt">The UTC time from which the record is never accepted.</param>
        public SamlCompletionRecord(string codeHash, string sealedOutcome, DateTime expiresAt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
            ArgumentException.ThrowIfNullOrWhiteSpace(sealedOutcome);

            CodeHash = codeHash;
            SealedOutcome = sealedOutcome;
            ExpiresAt = expiresAt;
        }

        /// <summary>
        /// The SHA-256 hash of the completion code, base64url-encoded: the record's key.
        /// </summary>
        public string CodeHash { get; }

        /// <summary>
        /// The sealed outcome, to store and return exactly as given.
        /// </summary>
        public string SealedOutcome { get; }

        /// <summary>
        /// The UTC time from which the record is never accepted.
        /// </summary>
        public DateTime ExpiresAt { get; }
    }
}
