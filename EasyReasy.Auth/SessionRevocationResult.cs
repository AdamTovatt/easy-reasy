namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the result of revoking one or more refresh-token families for a subject — whether from an
    /// explicit bulk revocation (password change, role demotion, admin-forced logout via
    /// <see cref="IRefreshTokenService.InvalidateAllSessionsAsync"/>) or from automatic
    /// <see cref="ConcurrentSessionPolicy.SingleSession"/> enforcement when a new login supersedes earlier ones.
    /// </summary>
    public sealed class SessionRevocationResult
    {
        /// <summary>
        /// The subject identifier (user id) whose sessions were revoked.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// The number of refresh token families that were invalidated by the operation.
        /// A value of zero means the subject had no active sessions at the time of the call.
        /// </summary>
        public int InvalidatedFamilyCount { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionRevocationResult"/> class.
        /// </summary>
        /// <param name="subject">The subject identifier (user id) whose sessions were revoked.</param>
        /// <param name="invalidatedFamilyCount">The number of refresh token families that were invalidated.</param>
        public SessionRevocationResult(string subject, int invalidatedFamilyCount)
        {
            Subject = subject;
            InvalidatedFamilyCount = invalidatedFamilyCount;
        }
    }
}
