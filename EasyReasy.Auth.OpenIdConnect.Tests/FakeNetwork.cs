namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The package's named HTTP client's only route out: each request goes to the fake issuer whose host it names, so a
    /// test can run several providers (an honest one and one passing off its responses) with no network.
    /// </summary>
    internal sealed class FakeNetwork : HttpMessageHandler
    {
        private readonly List<FakeOpenIdConnectIssuer> _issuers = new List<FakeOpenIdConnectIssuer>();
        private readonly List<Uri> _unroutedRequests = new List<Uri>();

        public void Add(FakeOpenIdConnectIssuer issuer)
        {
            _issuers.Add(issuer);
        }

        /// <summary>
        /// Every request any issuer received, in order.
        /// </summary>
        public int RequestCount => _issuers.Sum(issuer => issuer.RequestCount);

        /// <summary>
        /// Every request to a host no issuer serves, answered 404: an address the package should never have sent to.
        /// </summary>
        public IReadOnlyList<Uri> UnroutedRequests
        {
            get
            {
                lock (_unroutedRequests)
                {
                    return _unroutedRequests.ToArray();
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            FakeOpenIdConnectIssuer? issuer = _issuers.FirstOrDefault(candidate => candidate.Host == request.RequestUri!.Authority);
            if (issuer == null)
            {
                lock (_unroutedRequests)
                {
                    _unroutedRequests.Add(request.RequestUri!);
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            }

            return await issuer.HandleAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            // IHttpClientFactory disposes its handler chain when the handler's lifetime ends. The network is shared by the
            // test for its whole run, so it ignores that.
        }
    }
}
