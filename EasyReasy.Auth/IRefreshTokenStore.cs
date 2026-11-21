namespace EasyReasy.Auth
{
    /// <summary>
    /// Interface for consumer-implemented refresh token storage.
    /// The library stays database-agnostic — consumers implement this interface
    /// to persist refresh tokens in their preferred storage mechanism. The operations rotation needs come from
    /// <see cref="IRefreshTokenRotationStore"/>; this adds the subject-wide revocation of a first-party session.
    /// </summary>
    public interface IRefreshTokenStore : IRefreshTokenRotationStore
    {
        /// <summary>
        /// Invalidates every non-invalidated refresh token family for the specified subject.
        /// Used for bulk session revocation scenarios such as password change, role demotion,
        /// or admin-forced logout.
        /// </summary>
        /// <param name="subject">The subject (user identifier) whose sessions should be revoked.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// The number of token families that were invalidated by the operation. Implementations should
        /// count distinct families (not individual tokens) so that the value reflects the number of
        /// real sessions terminated — this flows into audit logs via <see cref="SessionRevocationResult"/>.
        /// </returns>
        Task<int> InvalidateAllFamiliesForUserAsync(string subject, CancellationToken cancellationToken = default);
    }
}
