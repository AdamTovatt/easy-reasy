using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// A fake HTTP message handler that returns preconfigured responses for testing.
    /// </summary>
    public class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new Queue<HttpResponseMessage>();
        private readonly List<HttpRequestMessage> _sentRequests = new List<HttpRequestMessage>();

        /// <summary>
        /// Gets the list of requests that were sent through this handler.
        /// </summary>
        public IReadOnlyList<HttpRequestMessage> SentRequests => _sentRequests;

        /// <summary>
        /// Whether to leave <see cref="HttpResponseMessage.RequestMessage"/> unset on the responses this handler
        /// returns, the way a handler that recorded none would.
        /// </summary>
        /// <remarks>
        /// Off by default, because the real handlers do set it. A handler that cannot be made to leave it unset is
        /// a handler that cannot reach the branch reporting an unknown endpoint, and that branch is a message a
        /// caller can be shown.
        /// </remarks>
        public bool OmitRequestMessage { get; set; }

        /// <summary>
        /// Enqueues a response to be returned by the next matching request.
        /// </summary>
        /// <param name="response">The response to return.</param>
        public void EnqueueResponse(HttpResponseMessage response)
        {
            _responses.Enqueue(response);
        }

        /// <summary>
        /// Enqueues a successful JSON response.
        /// </summary>
        /// <param name="json">The JSON content to return.</param>
        /// <param name="statusCode">The HTTP status code. Defaults to 200 OK.</param>
        public void EnqueueJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            HttpResponseMessage response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            _responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _sentRequests.Add(request);

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No more responses enqueued. Request: {request.Method} {request.RequestUri}");
            }

            HttpResponseMessage response = _responses.Dequeue();

            // The real handlers set this, and code that diagnoses a wrong host reads it — a fake that left it null
            // would report null where a running client reports an address, which is the whole of what is being tested.
            if (!OmitRequestMessage)
            {
                response.RequestMessage ??= request;
            }

            return Task.FromResult(response);
        }
    }
}
