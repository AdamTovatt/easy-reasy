namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the result of a logout operation.
    /// Use the static factory methods <see cref="Known"/> and <see cref="Unknown"/> to create instances.
    /// </summary>
    /// <remarks>
    /// Deliberately does not carry the raw refresh token, so that consumer code logging the whole result
    /// cannot accidentally leak secrets.
    /// </remarks>
    public sealed class LogoutResult
    {
        /// <summary>
        /// Whether the supplied refresh token matched a stored token and a family was invalidated.
        /// <c>false</c> means the token was null, empty, or unknown to the store — the operation was a no-op.
        /// </summary>
        public bool WasKnown { get; }

        /// <summary>
        /// The subject identifier (user id) associated with the invalidated family.
        /// Only populated when <see cref="WasKnown"/> is true.
        /// </summary>
        public string? Subject { get; }

        /// <summary>
        /// The family identifier that was invalidated.
        /// Only populated when <see cref="WasKnown"/> is true.
        /// </summary>
        public string? FamilyId { get; }

        private LogoutResult(bool wasKnown, string? subject, string? familyId)
        {
            WasKnown = wasKnown;
            Subject = subject;
            FamilyId = familyId;
        }

        /// <summary>
        /// Creates a result representing a logout that invalidated a known token family.
        /// </summary>
        /// <param name="subject">The subject identifier (user id) associated with the invalidated family.</param>
        /// <param name="familyId">The family identifier that was invalidated.</param>
        /// <returns>A <see cref="LogoutResult"/> with <see cref="WasKnown"/> set to <c>true</c>.</returns>
        public static LogoutResult Known(string subject, string familyId)
        {
            return new LogoutResult(true, subject, familyId);
        }

        /// <summary>
        /// Creates a result representing a logout call that did not match any stored token (null, empty, or unknown).
        /// </summary>
        /// <returns>A <see cref="LogoutResult"/> with <see cref="WasKnown"/> set to <c>false</c>.</returns>
        public static LogoutResult Unknown()
        {
            return new LogoutResult(false, null, null);
        }
    }
}
