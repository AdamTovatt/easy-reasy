using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Fetches and checks a client ID metadata document (draft-ietf-oauth-client-id-metadata-document-03, "the draft"
    /// below): a client whose id is an https URL publishes its metadata at that URL, and the server reads it there instead
    /// of from a registration.
    /// </summary>
    /// <remarks>
    /// A usable document is kept for <see cref="CacheLifetime"/>, as the draft recommends, so a client's repeated
    /// authorizations fetch it once. A failure is not kept, so a client fixing its document is not made to wait.
    /// </remarks>
    internal sealed class OAuthClientMetadataDocumentFetcher
    {
        /// <summary>
        /// The most bytes of a document read, the draft's recommended limit.
        /// </summary>
        public const int MaxDocumentBytes = 5 * 1024;

        /// <summary>
        /// How long a usable document is kept before it is fetched again.
        /// </summary>
        public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The most documents kept at once. Client ids are chosen by anonymous callers, so the cache is bounded: when it
        /// is full of live entries, a new document is used without being kept.
        /// </summary>
        public const int MaxCachedDocuments = 1000;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ConcurrentDictionary<string, CachedDocument> _cache = new ConcurrentDictionary<string, CachedDocument>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes the fetcher.
        /// </summary>
        /// <param name="httpClientFactory">The factory for <see cref="OAuthServerServiceCollectionExtensions.MetadataDocumentHttpClientName"/>.</param>
        /// <param name="timeProvider">The clock the cache runs on.</param>
        public OAuthClientMetadataDocumentFetcher(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
        {
            _httpClientFactory = httpClientFactory;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Returns the document a client id names: the one kept from an earlier fetch while it is live, or a fresh fetch,
        /// checked.
        /// </summary>
        /// <param name="clientId">The client id, already allowed by <see cref="OAuthClientIdUrl.IsValid"/>.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The client's metadata, or why it could not be used.</returns>
        public async Task<OAuthClientMetadataDocumentResult> FetchAsync(string clientId, CancellationToken cancellationToken)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (_cache.TryGetValue(clientId, out CachedDocument? cached) && cached.ExpiresAt > now)
            {
                return OAuthClientMetadataDocumentResult.Succeeded(cached.Metadata);
            }

            OAuthClientMetadataDocumentResult result = await FetchUncachedAsync(clientId, cancellationToken);
            if (result.IsUsable)
            {
                Keep(clientId, new CachedDocument(result.Metadata, now.Add(CacheLifetime)), now);
            }

            return result;
        }

        private void Keep(string clientId, CachedDocument document, DateTimeOffset now)
        {
            if (_cache.Count >= MaxCachedDocuments)
            {
                foreach (KeyValuePair<string, CachedDocument> entry in _cache)
                {
                    if (entry.Value.ExpiresAt <= now)
                    {
                        _cache.TryRemove(entry);
                    }
                }

                if (_cache.Count >= MaxCachedDocuments)
                {
                    return;
                }
            }

            _cache[clientId] = document;
        }

        private async Task<OAuthClientMetadataDocumentResult> FetchUncachedAsync(string clientId, CancellationToken cancellationToken)
        {
            HttpClient client = _httpClientFactory.CreateClient(OAuthServerServiceCollectionExtensions.MetadataDocumentHttpClientName);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, clientId);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            JsonDocument? document;
            try
            {
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                // Redirects are not followed (the draft forbids it), so a 3xx lands here as any other non-200.
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return OAuthClientMetadataDocumentResult.Failed($"The client ID metadata document answered {(int)response.StatusCode}, not 200.");
                }

                await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken);
                document = await OAuthJson.TryReadAsync(body, MaxDocumentBytes, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                return OAuthClientMetadataDocumentResult.Failed($"The client ID metadata document could not be fetched: {exception.Message}");
            }

            if (document == null)
            {
                return OAuthClientMetadataDocumentResult.Failed($"The client ID metadata document is not a JSON document of at most {MaxDocumentBytes} bytes.");
            }

            using (document)
            {
                return Check(clientId, document.RootElement);
            }
        }

        private static OAuthClientMetadataDocumentResult Check(string clientId, JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return OAuthClientMetadataDocumentResult.Failed("The client ID metadata document is not a JSON object.");
            }

            // Plain string comparison, no normalising: the draft forbids it.
            if (!root.TryGetProperty("client_id", out JsonElement clientIdElement)
                || clientIdElement.ValueKind != JsonValueKind.String
                || !string.Equals(clientIdElement.GetString(), clientId, StringComparison.Ordinal))
            {
                return OAuthClientMetadataDocumentResult.Failed("The client ID metadata document's client_id is not the URL it was fetched from.");
            }

            if (root.TryGetProperty("client_secret", out _) || root.TryGetProperty("client_secret_expires_at", out _))
            {
                return OAuthClientMetadataDocumentResult.Failed("A client ID metadata document must not carry a client secret.");
            }

            if (!OAuthClientMetadata.TryRead(root, out OAuthClientMetadata? metadata, out OAuthError? error))
            {
                return OAuthClientMetadataDocumentResult.Failed($"The client ID metadata document is not acceptable: {error.Description}");
            }

            // Absent means none (the draft overrides RFC 7591's client_secret_basic default). private_key_jwt is the
            // draft's other method, which this server does not support.
            if (metadata.AuthenticationMethodName != null && metadata.AuthenticationMethodName != "none")
            {
                return OAuthClientMetadataDocumentResult.Failed($"The client ID metadata document's token_endpoint_auth_method '{metadata.AuthenticationMethodName}' is not supported; only none is.");
            }

            return OAuthClientMetadataDocumentResult.Succeeded(metadata);
        }

        private sealed record CachedDocument(OAuthClientMetadata Metadata, DateTimeOffset ExpiresAt);
    }
}
