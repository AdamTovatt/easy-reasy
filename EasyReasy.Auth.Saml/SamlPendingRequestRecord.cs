namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// A sign-in begun and not yet answered, as <see cref="ISamlAuthStore"/> keeps it: found by the hash of the
    /// AuthnRequest's ID, which the identity provider's response names in <c>InResponseTo</c>.
    /// </summary>
    /// <remarks>
    /// The store holds the record as given and never reads inside it: <see cref="SealedState"/> is encrypted and
    /// authenticated with <see cref="SamlAuthOptions.StateKey"/>, so the store holds no identity provider key, rule or
    /// caller binding in plaintext, and a row written by anything but the package is refused.
    /// </remarks>
    public sealed class SamlPendingRequestRecord
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamlPendingRequestRecord"/> class.
        /// </summary>
        /// <param name="requestIdHash">The SHA-256 hash of the AuthnRequest's ID, lowercase hex, as <see cref="SecureToken.Hash"/> computes it.</param>
        /// <param name="sealedState">The sealed state of the sign-in.</param>
        /// <param name="expiresAt">The UTC time from which the record is never accepted.</param>
        public SamlPendingRequestRecord(string requestIdHash, string sealedState, DateTime expiresAt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requestIdHash);
            ArgumentException.ThrowIfNullOrWhiteSpace(sealedState);

            RequestIdHash = requestIdHash;
            SealedState = sealedState;
            ExpiresAt = expiresAt;
        }

        /// <summary>
        /// The SHA-256 hash of the AuthnRequest's ID, lowercase hex, as <see cref="SecureToken.Hash"/> computes it: the record's key.
        /// </summary>
        public string RequestIdHash { get; }

        /// <summary>
        /// The sealed state of the sign-in, to store and return exactly as given.
        /// </summary>
        public string SealedState { get; }

        /// <summary>
        /// The UTC time from which the record is never accepted.
        /// </summary>
        public DateTime ExpiresAt { get; }
    }
}
