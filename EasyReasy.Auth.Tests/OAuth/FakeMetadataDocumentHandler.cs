using System.Net;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Stands in for the internet when the server fetches a client ID metadata document: answers each URL with the status
    /// and body a test set, and records the requests.
    /// </summary>
    internal sealed class FakeMetadataDocumentHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _documents = new Dictionary<string, (HttpStatusCode, string)>(StringComparer.Ordinal);

        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        public void Serve(string url, string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _documents[url] = (status, body);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (!_documents.TryGetValue(request.RequestUri!.OriginalString, out (HttpStatusCode Status, string Body) document))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(document.Status)
            {
                Content = new StringContent(document.Body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
