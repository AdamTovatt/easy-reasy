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

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
