namespace EasyReasy.Auth
{
    /// <summary>
    /// Finds the client an authorization request names: a client ID metadata document URL is fetched, any other id is
    /// looked up in the store.
    /// </summary>
    internal sealed class OAuthClientDirectory
    {
        private readonly OAuthServerSettings _settings;
        private readonly IOAuthServerStore _store;
        private readonly OAuthClientMetadataDocumentFetcher _fetcher;

        /// <summary>
        /// Initializes the directory.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <param name="store">The application's store.</param>
        /// <param name="fetcher">The metadata document fetcher.</param>
        public OAuthClientDirectory(OAuthServerSettings settings, IOAuthServerStore store, OAuthClientMetadataDocumentFetcher fetcher)
        {
            _settings = settings;
            _store = store;
            _fetcher = fetcher;
        }

        /// <summary>
        /// Finds a client.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The client, or why there is none.</returns>
        public async Task<OAuthClientLookup> FindAsync(string clientId, CancellationToken cancellationToken)
        {
            if (OAuthClientMetadataDocumentFetcher.IsMetadataDocumentClientId(clientId))
            {
                if (!_settings.AllowClientIdMetadataDocuments)
                {
                    return OAuthClientLookup.Failed("This server does not accept client ID metadata documents.");
                }

                if (!OAuthClientMetadataDocumentFetcher.IsValidClientIdUrl(clientId))
                {
                    return OAuthClientLookup.Failed("The client_id is not a valid client ID metadata document URL: https, with a path other than /, no userinfo, dot segments or fragment.");
                }

                OAuthClientMetadataDocumentResult document = await _fetcher.FetchAsync(clientId, cancellationToken);
                if (!document.IsUsable)
                {
                    return OAuthClientLookup.Failed(document.Failure);
                }

                OAuthClientInfo info = OAuthClientInfo.Create(clientId, OAuthClientRegistrationKind.MetadataDocument, document.Metadata.ClientName, document.Metadata.ClientUri);
                return OAuthClientLookup.Found(new OAuthResolvedClient(info, document.Metadata.RedirectUris));
            }

            OAuthClient? client = await _store.GetClientAsync(clientId, cancellationToken);
            if (client == null)
            {
                return OAuthClientLookup.Failed("The client_id is not a registered client.");
            }

            OAuthClientInfo registeredInfo = OAuthClientInfo.Create(client.ClientId, OAuthClientRegistrationKind.Registered, client.ClientName, client.ClientUri);
            return OAuthClientLookup.Found(new OAuthResolvedClient(registeredInfo, client.RedirectUris));
        }
    }
}
