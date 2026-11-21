namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// An authorization request <see cref="OAuthTestClient.AuthorizeAsync"/> made and the server is holding for the consent
    /// page, with what the client keeps to finish the flow. A record, so a test changes one value with <c>with</c>.
    /// </summary>
    public sealed record OAuthTestAuthorization
    {
        /// <summary>
        /// The client that asked.
        /// </summary>
        public required OAuthTestRegisteredClient Client { get; init; }

        /// <summary>
        /// The pending request's id, from the consent page URL's <c>request</c> parameter.
        /// </summary>
        public required string RequestId { get; init; }

        /// <summary>
        /// The consent page URL the server sent the browser to.
        /// </summary>
        public required string ConsentPageUrl { get; init; }

        /// <summary>
        /// The redirect URI the request named, which the approval's redirect goes to and the code's redemption names.
        /// </summary>
        public required string RedirectUri { get; init; }

        /// <summary>
        /// The PKCE code verifier whose challenge the request carried.
        /// </summary>
        public required string CodeVerifier { get; init; }

        /// <summary>
        /// The <c>state</c> the request carried, which the redirect must return.
        /// </summary>
        public required string State { get; init; }

        /// <summary>
        /// The <c>resource</c> the request named, or null.
        /// </summary>
        public string? Resource { get; init; }
    }
}
