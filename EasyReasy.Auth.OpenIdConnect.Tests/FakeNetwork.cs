namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The package's named HTTP client's only route out: each request goes to the fake issuer whose host it names, so a
    /// test can run several providers (an honest one and one passing off its responses) with no network.
    /// </summary>
    internal sealed class FakeNetwork : HttpMessageHandler
    {
        private readonly List<FakeOpenIdConnectIssuer> _issuers = new List<FakeOpenIdConnectIssuer>();

        public void Add(FakeOpenIdConnectIssuer issuer)
        {
            _issuers.Add(issuer);
        }

        /// <summary>
        /// Every request any issuer received, in order.
        /// </summary>
        public int RequestCount => _issuers.Sum(issuer => issuer.RequestCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            FakeOpenIdConnectIssuer? issuer = _issuers.FirstOrDefault(candidate => candidate.Host == request.RequestUri!.Authority);
            return issuer == null
                ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                : await issuer.HandleAsync(request, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            // IHttpClientFactory disposes its handler chain when the handler's lifetime ends. The network is shared by the
            // test for its whole run, so it ignores that.
        }
    }
}
