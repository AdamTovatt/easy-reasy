namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The application's storage for SAML sign-ins in progress: the requests begun and not yet answered, and the outcomes
    /// the assertion consumer service checked and the completion page has not yet redeemed, each one
    /// <see cref="SamlAuthRecord"/>. The package stays database-agnostic, as with <see cref="IOAuthServerStore"/>. Every
    /// record holds only its kind, a hash, a sealed value and an expiry.
    /// </summary>
    /// <remarks>
    /// <see cref="ConsumeAsync"/> must be atomic: it returns a record's sealed value to the first caller only and removes
    /// the record, so a second caller with the same kind and hash gets null (in SQL, a <c>DELETE ... WHERE kind = @kind
    /// AND hash = @hash AND expires_at &gt; @now RETURNING</c> the sealed column, or an <c>UPDATE</c> that marks the row
    /// used and a check that one row was affected). That is what makes a response usable once, so a captured response or
    /// code replayed later is refused. Every write must be committed when its task completes. The package deletes expired
    /// rows itself through <see cref="DeleteExpiredAsync"/>.
    /// </remarks>
    public interface ISamlAuthStore
    {
        /// <summary>
        /// Stores a record. Its kind and hash are its key.
        /// </summary>
        /// <param name="record">The record to store.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task StoreAsync(SamlAuthRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically removes a record and returns its sealed value.
        /// </summary>
        /// <param name="kind">The record's kind.</param>
        /// <param name="hash">The record's hash.</param>
        /// <param name="now">The UTC time; a record whose <see cref="SamlAuthRecord.ExpiresAt"/> is at or before it is
        /// never returned.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The record's <see cref="SamlAuthRecord.SealedValue"/> for the first caller only; null when it was
        /// already consumed, expired or never stored.</returns>
        Task<string?> ConsumeAsync(SamlAuthRecordKind kind, string hash, DateTime now, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every record whose <see cref="SamlAuthRecord.ExpiresAt"/> is at or before <paramref name="now"/>. The
        /// package's sweep calls it every <see cref="SamlAuthOptions.SweepInterval"/>; every app instance runs its own
        /// sweep, so calls can overlap and each must be safe beside another.
        /// </summary>
        /// <param name="now">The UTC time rows are compared with.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default);
    }
}
