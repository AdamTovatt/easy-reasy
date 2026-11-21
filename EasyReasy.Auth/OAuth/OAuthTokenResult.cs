namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of a token request, reported to <see cref="IAuthAuditLogger.OnOAuthTokenAsync"/>. It never carries a
    /// code or token.
    /// </summary>
    public sealed class OAuthTokenResult
    {
        /// <summary>
        /// Whether tokens were issued.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// The grant type, or null when the request named none the server supports.
        /// </summary>
        public OAuthGrantType? GrantType { get; init; }

        /// <summary>
        /// The client id the request named, authenticated or not; null when it named none. Check
        /// <see cref="FailureReason"/> before trusting it: on <see cref="OAuthTokenFailureReason.InvalidClient"/> it is
        /// only what the caller claimed.
        /// </summary>
        public string? ClientId { get; init; }

        /// <summary>
        /// The grant the code or refresh token belongs to, when one was found.
        /// </summary>
        public string? GrantId { get; init; }

        /// <summary>
        /// The subject the tokens are for, when a code or refresh token was found.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// The refresh token family, when a code or refresh token was found.
        /// </summary>
        public string? FamilyId { get; init; }

        /// <summary>
        /// Why the request was refused, or null on success.
        /// </summary>
        public OAuthTokenFailureReason? FailureReason { get; init; }
    }
}
