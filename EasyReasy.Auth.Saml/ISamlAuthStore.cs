namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The application's storage for SAML sign-ins in progress: the requests begun and not yet answered, and the outcomes
    /// the assertion consumer service checked and the completion page has not yet redeemed. The package stays
    /// database-agnostic, as with <see cref="IOAuthServerStore"/>. Every record holds only a hash and a sealed value.
    /// </summary>
    /// <remarks>
    /// The two consume methods must be atomic: each returns a record's sealed value to the first caller only and removes
    /// the record, so a second caller with the same hash gets null (in SQL, a <c>DELETE ... WHERE hash = @hash AND
    /// expires_at &gt; @now RETURNING</c> the sealed column, or an <c>UPDATE</c> that marks the row used and a check that
    /// one row was affected). They are what makes a response usable once, so a captured response or code replayed later is refused. Every write must be
    /// committed when its task completes. The package deletes expired rows itself through <see cref="DeleteExpiredAsync"/>.
    /// </remarks>
    public interface ISamlAuthStore
    {
        /// <summary>
        /// Stores a request begun and not yet answered.
        /// </summary>
        /// <param name="record">The record to store.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task StorePendingRequestAsync(SamlPendingRequestRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically removes a pending request and returns its sealed state.
        /// </summary>
        /// <param name="requestIdHash">The SHA-256 hash of the AuthnRequest's ID.</param>
        /// <param name="now">The UTC time; a record whose <see cref="SamlPendingRequestRecord.ExpiresAt"/> is at or before
        /// it is never returned.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The record's <see cref="SamlPendingRequestRecord.SealedState"/> for the first caller only; null when it
        /// was already consumed, expired or never stored.</returns>
        Task<string?> ConsumePendingRequestAsync(string requestIdHash, DateTime now, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores an outcome for the completion page to redeem.
        /// </summary>
        /// <param name="record">The record to store.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task StoreCompletionAsync(SamlCompletionRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically removes a completion and returns its sealed outcome.
        /// </summary>
        /// <param name="codeHash">The SHA-256 hash of the completion code.</param>
        /// <param name="now">The UTC time; a record whose <see cref="SamlCompletionRecord.ExpiresAt"/> is at or before it
        /// is never returned.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The record's <see cref="SamlCompletionRecord.SealedOutcome"/> for the first caller only; null when it was
        /// already consumed, expired or never stored.</returns>
        Task<string?> ConsumeCompletionAsync(string codeHash, DateTime now, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes every pending request and completion whose <c>ExpiresAt</c> is at or before <paramref name="now"/>.
        /// The package's sweep calls it every <see cref="SamlAuthOptions.SweepInterval"/>; every app instance runs its own sweep,
        /// so calls can overlap and each must be safe beside another.
        /// </summary>
        /// <param name="now">The UTC time rows are compared with.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default);
    }
}
