using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Fetches and checks a client ID metadata document (draft-ietf-oauth-client-id-metadata-document-03, the revision
    /// the MCP authorization specification of 2025-11-25 cites; "the draft" below): a client whose id
    /// is an https URL publishes its metadata at that URL, and the server reads it there instead of from a registration.
    /// </summary>
    internal sealed class OAuthClientMetadataDocumentFetcher
    {
        /// <summary>
        /// The most bytes of a document read, the draft's recommended limit.
        /// </summary>
        public const int MaxDocumentBytes = 5 * 1024;

        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes the fetcher.
        /// </summary>
        /// <param name="httpClientFactory">The factory for <see cref="OAuthServerServiceCollectionExtensions.MetadataDocumentHttpClientName"/>.</param>
        public OAuthClientMetadataDocumentFetcher(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Whether a client id is written as a client ID metadata document URL (starts with <c>https://</c>), so it is
        /// looked up by fetching, never in the store. A registered client's id is random base64url and never has a colon.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <returns>Whether it names a metadata document.</returns>
        public static bool IsMetadataDocumentClientId(string clientId)
        {
            return clientId.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether a client id URL meets the rules draft-ietf-oauth-client-id-metadata-document-03 sets for one: https,
        /// no userinfo, a path other than <c>/</c>, no dot segments, no fragment.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <returns>Whether it is allowed.</returns>
        public static bool IsValidClientIdUrl(string clientId)
        {
            if (clientId.Length > OAuthRedirectUri.MaxLength
                || OAuthUrl.HasFragment(clientId)
                || !Uri.TryCreate(clientId, UriKind.Absolute, out Uri? parsed)
                || parsed.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(parsed.UserInfo)
                || parsed.AbsolutePath == "/")
            {
                return false;
            }

            // Uri removes dot segments while parsing, so they are looked for in what the client sent.
            string afterScheme = clientId.Substring("https://".Length);
            int pathStart = afterScheme.IndexOf('/');
            string path = pathStart < 0 ? "" : afterScheme.Substring(pathStart);
            int queryStart = path.IndexOf('?');
            if (queryStart >= 0)
            {
                path = path.Substring(0, queryStart);
            }

            foreach (string segment in path.Split('/'))
            {
                string decoded = segment.Replace("%2e", ".", StringComparison.OrdinalIgnoreCase);
                if (decoded == "." || decoded == "..")
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Fetches and checks the document a client id names.
        /// </summary>
        /// <param name="clientId">The client id, already allowed by <see cref="IsValidClientIdUrl"/>.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The client's metadata, or why it could not be used.</returns>
        public async Task<OAuthClientMetadataDocumentResult> FetchAsync(string clientId, CancellationToken cancellationToken)
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
    }
}
