namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The identity in a validated Microsoft ID token.
    /// </summary>
    /// <remarks>
    /// Link accounts by <see cref="TenantId"/> and <see cref="ObjectId"/> together. Never use
    /// <see cref="PreferredUsername"/> or an email address as an identifier: a tenant administrator can change it,
    /// and another tenant can hold the same one.
    /// </remarks>
    public sealed class MicrosoftUserInfo
    {
        /// <summary>
        /// The tenant the account belongs to (the <c>tid</c> claim).
        /// </summary>
        public required Guid TenantId { get; init; }

        /// <summary>
        /// The account's immutable id within its tenant (the <c>oid</c> claim).
        /// </summary>
        public required string ObjectId { get; init; }

        /// <summary>
        /// When the user last entered credentials at Microsoft (the <c>auth_time</c> claim), or null when the
        /// token does not carry it. The app registration must include <c>auth_time</c> as an optional ID token claim.
        /// </summary>
        public DateTimeOffset? AuthenticationTime { get; init; }

        /// <summary>
        /// The nonce the token was issued for: the caller's own when the flow's Begin supplied one, otherwise the
        /// random one Begin generated.
        /// </summary>
        public required string Nonce { get; init; }

        /// <summary>
        /// The account's sign-in name (the <c>preferred_username</c> claim), for display only, or null.
        /// </summary>
        public string? PreferredUsername { get; init; }

        /// <summary>
        /// The account's display name (the <c>name</c> claim), or null.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// The authentication methods the token reports (the <c>amr</c> claim) as given, possibly empty.
        /// Informational: it cannot prove a second factor ran at this sign-in. Require an authentication context
        /// for that.
        /// </summary>
        public required IReadOnlyList<string> AuthenticationMethods { get; init; }

        /// <summary>
        /// The Conditional Access authentication context ids the token carries (the <c>acrs</c> claim), possibly
        /// empty. The app registration must include <c>acrs</c> as an optional ID token claim.
        /// </summary>
        public required IReadOnlyList<string> AuthenticationContextIds { get; init; }
    }
}
