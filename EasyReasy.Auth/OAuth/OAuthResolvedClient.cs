namespace EasyReasy.Auth
{
    /// <summary>
    /// A client the authorization endpoint found, registered or by metadata document, with what it registered.
    /// </summary>
    internal sealed class OAuthResolvedClient
    {
        /// <summary>
        /// Initializes the client.
        /// </summary>
        /// <param name="info">What the consent page shows.</param>
        /// <param name="redirectUris">The registered redirect URIs.</param>
        public OAuthResolvedClient(OAuthClientInfo info, IReadOnlyList<string> redirectUris)
        {
            Info = info;
            RedirectUris = redirectUris;
        }

        /// <summary>
        /// What the consent page shows.
        /// </summary>
        public OAuthClientInfo Info { get; }

        /// <summary>
        /// The registered redirect URIs.
        /// </summary>
        public IReadOnlyList<string> RedirectUris { get; }

        /// <summary>
        /// Whether a request's redirect URI matches a registered one.
        /// </summary>
        /// <param name="redirectUri">The request's redirect URI.</param>
        /// <returns>Whether it matches.</returns>
        public bool AllowsRedirectUri(string redirectUri)
        {
            return RedirectUris.Any(registered => OAuthRedirectUri.Matches(registered, redirectUri));
        }
    }
}
