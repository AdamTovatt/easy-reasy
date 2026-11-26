using Microsoft.IdentityModel.Protocols;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Fetches discovery documents and key sets through the package's named HTTP client, so the client's handler is
    /// managed by <see cref="IHttpClientFactory"/> and a test can stand a fake provider in.
    /// </summary>
    internal sealed class HttpClientFactoryDocumentRetriever : IDocumentRetriever
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _metadataAddress;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientFactoryDocumentRetriever"/> class for one provider's
        /// documents.
        /// </summary>
        /// <param name="httpClientFactory">Creates the package's named HTTP client.</param>
        /// <param name="metadataAddress">The provider's discovery address, which decides whether plain http to a loopback
        /// host is allowed for the addresses its document names.</param>
        public HttpClientFactoryDocumentRetriever(IHttpClientFactory httpClientFactory, string metadataAddress)
        {
            _httpClientFactory = httpClientFactory;
            _metadataAddress = metadataAddress;
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">The address is not https (or http on a loopback host, for a provider whose
        /// discovery address is one). The keys an ID token is checked against must not travel unprotected, wherever a
        /// discovery document points.</exception>
        /// <exception cref="HttpRequestException">The document could not be fetched.</exception>
        public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            if (!OpenIdConnectAddress.TryParseFetchableFrom(address, _metadataAddress, out Uri? uri))
            {
                throw new ArgumentException($"Refusing to fetch '{address}': only https is allowed, or http on a loopback host for a provider whose discovery address is one.", nameof(address));
            }

            HttpClient client = _httpClientFactory.CreateClient(OpenIdConnectCodeFlow.HttpClientName);
            using HttpResponseMessage response = await client.GetAsync(uri, cancel);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancel);
        }
    }
}
