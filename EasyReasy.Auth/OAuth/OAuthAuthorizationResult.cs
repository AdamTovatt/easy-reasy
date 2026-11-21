namespace EasyReasy.Auth
{
    /// <summary>
    /// A person's decision on a pending authorization request, reported to
    /// <see cref="IAuthAuditLogger.OnOAuthAuthorizationAsync"/>. The approving person is the request's
    /// <c>HttpContext.User</c>.
    /// </summary>
    public sealed class OAuthAuthorizationResult
    {
        /// <summary>
        /// How it ended.
        /// </summary>
        public required OAuthAuthorizationOutcome Outcome { get; init; }

        /// <summary>
        /// The client asking to connect.
        /// </summary>
        public required OAuthClientInfo Client { get; init; }

        /// <summary>
        /// The grant recorded, on <see cref="OAuthAuthorizationOutcome.Approved"/>.
        /// </summary>
        public string? GrantId { get; init; }

        /// <summary>
        /// The subject the tokens will be for, on <see cref="OAuthAuthorizationOutcome.Approved"/>.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// The handler's reason, on <see cref="OAuthAuthorizationOutcome.Refused"/>, when it gave one.
        /// </summary>
        public string? RefusalDetail { get; init; }
    }
}
