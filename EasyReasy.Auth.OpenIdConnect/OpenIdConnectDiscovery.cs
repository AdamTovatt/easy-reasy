using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Collections.Concurrent;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The providers' discovery documents and signing keys, each cached and refreshed by its own
    /// <see cref="ConfigurationManager{T}"/>, so an ordinary sign-in does not fetch them again.
    /// </summary>
    /// <remarks>
    /// An entry is keyed by the address the document is read from and the issuer it must name, so a provider whose
    /// issuer or address changes gets a new entry rather than the old document. A document is checked before it is
    /// cached: its issuer must be the provider's, its authorize endpoint https (or http on a host the browser treats as
    /// secure), and its token endpoint https (or http on a loopback host, for a provider whose discovery address is one).
    /// One that fails is not cached, and the flow fails with <see cref="InvalidOperationException"/>.
    /// </remarks>
    internal sealed class OpenIdConnectDiscovery
    {
        private readonly ConcurrentDictionary<CacheKey, IConfigurationManager<OpenIdConnectConfiguration>> _managers = new ConcurrentDictionary<CacheKey, IConfigurationManager<OpenIdConnectConfiguration>>();
        private readonly IConfigurationRetriever<OpenIdConnectConfiguration> _configurationRetriever = new OpenIdConnectConfigurationRetriever();
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectDiscovery"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Creates the package's named HTTP client, which fetches the documents.</param>
        public OpenIdConnectDiscovery(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// The provider's discovery document and keys, from the cache when it holds them.
        /// </summary>
        /// <param name="provider">The provider, already checked.</param>
        /// <param name="cancellationToken">Cancels a fetch.</param>
        /// <returns>The checked document.</returns>
        /// <exception cref="InvalidOperationException">It could not be retrieved, or is not one the provider may use.</exception>
        public Task<OpenIdConnectConfiguration> GetAsync(OpenIdConnectProvider provider, CancellationToken cancellationToken)
        {
            return GetManager(provider).GetConfigurationAsync(cancellationToken);
        }

        /// <summary>
        /// Reads the provider's discovery document and keys now, bypassing the cache, and asks the cache to refresh.
        /// </summary>
        /// <remarks>
        /// Once a <see cref="ConfigurationManager{T}"/> holds a document, <c>RequestRefresh</c> updates it in the
        /// background and <c>GetConfigurationAsync</c> keeps returning the held one, so asking it again now would hand back
        /// the old keys. The refresh brings the cache up to date for later flows, and this flow reads the document directly
        /// through the same retrievers and the same check.
        /// </remarks>
        /// <param name="provider">The provider, already checked.</param>
        /// <param name="cancellationToken">Cancels the fetch.</param>
        /// <returns>The checked document.</returns>
        /// <exception cref="InvalidOperationException">It could not be retrieved, or is not one the provider may use.</exception>
        public async Task<OpenIdConnectConfiguration> FetchAsync(OpenIdConnectProvider provider, CancellationToken cancellationToken)
        {
            GetManager(provider).RequestRefresh();
            string address = provider.Issuer.MetadataAddress;

            OpenIdConnectConfiguration configuration;
            try
            {
                configuration = await _configurationRetriever.GetConfigurationAsync(address, new HttpClientFactoryDocumentRetriever(_httpClientFactory, address), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException($"The OpenID configuration could not be retrieved from '{address}'.", exception);
            }

            ConfigurationValidationResult check = new DocumentValidator(provider.Issuer.Value, address).Validate(configuration);
            if (!check.Succeeded)
            {
                throw new InvalidOperationException($"The OpenID configuration at '{address}' cannot be used: {check.ErrorMessage}");
            }

            return configuration;
        }

        private IConfigurationManager<OpenIdConnectConfiguration> GetManager(OpenIdConnectProvider provider)
        {
            return _managers.GetOrAdd(
                new CacheKey(provider.Issuer.MetadataAddress, provider.Issuer.Value),
                key => new ConfigurationManager<OpenIdConnectConfiguration>(
                    key.MetadataAddress,
                    _configurationRetriever,
                    new HttpClientFactoryDocumentRetriever(_httpClientFactory, key.MetadataAddress),
                    new DocumentValidator(key.Issuer, key.MetadataAddress)));
        }

        private readonly record struct CacheKey(string MetadataAddress, string Issuer);

        /// <summary>
        /// The check a discovery document must pass before it is used or cached.
        /// </summary>
        private sealed class DocumentValidator : IConfigurationValidator<OpenIdConnectConfiguration>
        {
            private readonly string _issuer;
            private readonly string _metadataAddress;

            public DocumentValidator(string issuer, string metadataAddress)
            {
                _issuer = issuer;
                _metadataAddress = metadataAddress;
            }

            public ConfigurationValidationResult Validate(OpenIdConnectConfiguration configuration)
            {
                List<string> problems = new List<string>();

                if (!string.Equals(configuration.Issuer, _issuer, StringComparison.Ordinal))
                {
                    problems.Add($"its issuer '{configuration.Issuer}' is not the provider's issuer '{_issuer}'");
                }

                if (!OpenIdConnectAddress.TryParseBrowserFacing(configuration.AuthorizationEndpoint, out Uri? _))
                {
                    problems.Add($"its authorization_endpoint '{configuration.AuthorizationEndpoint}' is not an absolute https URL (or http on a local host)");
                }

                if (!OpenIdConnectAddress.TryParseFetchableFrom(configuration.TokenEndpoint, _metadataAddress, out Uri? _))
                {
                    problems.Add($"its token_endpoint '{configuration.TokenEndpoint}' is not an absolute https URL (or http on a loopback host, for a provider whose discovery address is one)");
                }

                return problems.Count == 0
                    ? new ConfigurationValidationResult { Succeeded = true }
                    : new ConfigurationValidationResult { Succeeded = false, ErrorMessage = string.Join("; ", problems) + "." };
            }
        }
    }
}
