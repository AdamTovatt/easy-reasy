namespace EasyReasy.Auth
{
    /// <summary>
    /// An authorization request the server has validated and is holding while the person signs in and decides on the
    /// application's consent page. It is completed once, by an approval or a denial.
    /// </summary>
    public sealed class OAuthAuthorizationRequest
    {
        /// <summary>
        /// The SHA-256 hash (lowercase hex) of the request id the consent page carries. The primary lookup key; the raw
        /// id is never stored.
        /// </summary>
        public required string RequestHash { get; init; }

        /// <summary>
        /// The client that asked.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// The client's self-asserted name when the request was made, or null. Kept on the request so the consent page
        /// shows what was validated, and a metadata document is fetched once per request.
        /// </summary>
        public string? ClientName { get; init; }

        /// <summary>
        /// The client's self-asserted home page when the request was made, or null.
        /// </summary>
        public string? ClientUri { get; init; }

        /// <summary>
        /// The redirect URI the request named, already matched against the client's registered ones.
        /// </summary>
        public required string RedirectUri { get; init; }

        /// <summary>
        /// The client's <c>state</c>, returned to it unchanged on the redirect, or null.
        /// </summary>
        public string? State { get; init; }

        /// <summary>
        /// The PKCE code challenge (S256), which the token request's <c>code_verifier</c> must hash to.
        /// </summary>
        public required string CodeChallenge { get; init; }

        /// <summary>
        /// The requested scope, space-delimited as on the wire, or null when none was requested.
        /// </summary>
        public string? Scope { get; init; }

        /// <summary>
        /// The <c>resource</c> the client named (RFC 8707), or null when it named none.
        /// </summary>
        public string? Resource { get; init; }

        /// <summary>
        /// The UTC time after which the request can no longer be approved or denied.
        /// </summary>
        public required DateTime ExpiresAt { get; init; }

        /// <summary>
        /// The UTC time the request was approved or denied, or null while it is pending.
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }
}
