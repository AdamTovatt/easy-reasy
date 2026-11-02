using System.Net;

namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// A fake HTTP message handler that returns preconfigured responses for testing.
    /// </summary>
    public class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new Queue<Func<CancellationToken, Task<HttpResponseMessage>>>();
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
            Enqueue(_ => Task.FromResult(response));
        }

        /// <summary>
        /// Enqueues a response that the next request receives only once the returned gate is released, so a test
        /// can hold a request in flight while it starts other calls.
        /// </summary>
        /// <param name="response">The response to return once released.</param>
        /// <returns>The gate holding the request.</returns>
        public HandlerGate EnqueueGatedResponse(HttpResponseMessage response)
        {
            HandlerGate gate = new HandlerGate();
            Enqueue(async cancellationToken =>
            {
                await gate.PassAsync(cancellationToken);
                return response;
            });
            return gate;
        }

        /// <summary>
        /// Enqueues a failure that the next request throws only once the returned gate is released.
        /// </summary>
        /// <param name="exception">The exception to throw once released.</param>
        /// <returns>The gate holding the request.</returns>
        public HandlerGate EnqueueGatedFailure(Exception exception)
        {
            HandlerGate gate = new HandlerGate();
            Enqueue(async cancellationToken =>
            {
                await gate.PassAsync(cancellationToken);
                throw exception;
            });
            return gate;
        }

        /// <summary>
        /// Enqueues a failure to be thrown by the next request instead of a response, the way a handler throws
        /// when the server cannot be reached.
        /// </summary>
        /// <param name="exception">The exception to throw.</param>
        public void EnqueueFailure(Exception exception)
        {
            Enqueue(_ => throw exception);
        }

        private void Enqueue(Func<CancellationToken, Task<HttpResponseMessage>> next)
        {
            // Locked like the dequeue in SendAsync, because concurrency tests send requests from several callers.
            lock (_responses)
            {
                _responses.Enqueue(next);
            }
        }

        /// <summary>
        /// Enqueues a successful JSON response.
        /// </summary>
        /// <param name="json">The JSON content to return.</param>
        /// <param name="statusCode">The HTTP status code. Defaults to 200 OK.</param>
        public void EnqueueJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            EnqueueResponse(CreateJsonResponse(json, statusCode));
        }

        /// <summary>
        /// Enqueues a successful JSON response that the next request receives only once the returned gate is
        /// released.
        /// </summary>
        /// <param name="json">The JSON content to return.</param>
        /// <returns>The gate holding the request.</returns>
        public HandlerGate EnqueueGatedJsonResponse(string json)
        {
            return EnqueueGatedResponse(CreateJsonResponse(json, HttpStatusCode.OK));
        }

        private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<HttpResponseMessage>> next;

            lock (_responses)
            {
                _sentRequests.Add(request);

                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"No more responses enqueued. Request: {request.Method} {request.RequestUri}");
                }

                next = _responses.Dequeue();
            }

            HttpResponseMessage response = await next(cancellationToken);

            // The real handlers set this, and code that diagnoses a wrong host reads it — a fake that left it null
            // would report null where a running client reports an address, which is the whole of what is being tested.
            if (!OmitRequestMessage)
            {
                response.RequestMessage ??= request;
            }

            return response;
        }
    }
}
