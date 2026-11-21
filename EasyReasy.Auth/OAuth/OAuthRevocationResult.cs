namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of a client's revocation request, reported to <see cref="IAuthAuditLogger.OnOAuthRevocationAsync"/>
    /// after the client authenticated. The client is answered 200 either way (RFC 7009 section 2.2), so the response does
    /// not reveal which it was.
    /// </summary>
    public sealed class OAuthRevocationResult
    {
        /// <summary>
        /// The authenticated client.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// Whether the token was a refresh token bound to this client, whose family is now invalidated. False for an
        /// unknown token, another client's, or an access token (which cannot be revoked and expires on its own).
        /// </summary>
        public required bool WasKnown { get; init; }

        /// <summary>
        /// The grant of the revoked token, when known.
        /// </summary>
        public string? GrantId { get; init; }

        /// <summary>
        /// The subject of the revoked token, when known.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// The invalidated family, when known.
        /// </summary>
        public string? FamilyId { get; init; }
    }
}
