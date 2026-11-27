namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// A single-use record of a SAML sign-in in progress, as <see cref="ISamlAuthStore"/> keeps it: its kind, the hash it
    /// is found by, its sealed value and when it expires.
    /// </summary>
    /// <remarks>
    /// The store holds the record as given and never reads inside it: <see cref="SealedValue"/> is encrypted and
    /// authenticated with <see cref="SamlAuthOptions.StateKey"/>, so the store holds no identity, identity provider key,
    /// rule or caller binding in plaintext, and a row written by anything but the package is refused.
    /// </remarks>
    public sealed class SamlAuthRecord
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamlAuthRecord"/> class.
        /// </summary>
        /// <param name="kind">What the record holds.</param>
        /// <param name="hash">The SHA-256 hash it is found by, lowercase hex, as <see cref="SecureToken.Hash"/> computes
        /// it: of the AuthnRequest's ID for a <see cref="SamlAuthRecordKind.PendingRequest"/>, of the completion code for a
        /// <see cref="SamlAuthRecordKind.Completion"/>.</param>
        /// <param name="sealedValue">The sealed state or outcome.</param>
        /// <param name="expiresAt">The UTC time from which the record is never accepted.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is no member.</exception>
        /// <exception cref="ArgumentException"><paramref name="hash"/> or <paramref name="sealedValue"/> is null or
        /// blank.</exception>
        public SamlAuthRecord(SamlAuthRecordKind kind, string hash, string sealedValue, DateTime expiresAt)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "The record kind is no member.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(hash);
            ArgumentException.ThrowIfNullOrWhiteSpace(sealedValue);

            Kind = kind;
            Hash = hash;
            SealedValue = sealedValue;
            ExpiresAt = expiresAt;
        }

        /// <summary>
        /// What the record holds. With <see cref="Hash"/>, the record's key.
        /// </summary>
        public SamlAuthRecordKind Kind { get; }

        /// <summary>
        /// The SHA-256 hash the record is found by, lowercase hex. With <see cref="Kind"/>, the record's key.
        /// </summary>
        public string Hash { get; }

        /// <summary>
        /// The sealed state or outcome, to store and return exactly as given.
        /// </summary>
        public string SealedValue { get; }

        /// <summary>
        /// The UTC time from which the record is never accepted.
        /// </summary>
        public DateTime ExpiresAt { get; }
    }
}
