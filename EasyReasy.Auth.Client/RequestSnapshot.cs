using System.Net.Http.Headers;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// What the caller set on a request, captured before it is first sent, so that a copy of it can be sent again
    /// after a <c>401</c>: a request message cannot be sent twice.
    /// </summary>
    /// <remarks>
    /// Captured before the send because sending changes the message: <see cref="HttpClient"/> merges its default
    /// headers into it, the token the server is about to reject among them, and handlers may write their own
    /// per-request state into <see cref="HttpRequestMessage.Options"/>. A copy taken afterwards would carry both.
    /// The content object itself is shared, not copied, so nothing is buffered.
    /// </remarks>
    internal sealed class RequestSnapshot
    {
        private readonly HttpMethod _method;
        private readonly Uri? _requestUri;
        private readonly Version _version;
        private readonly HttpVersionPolicy _versionPolicy;
        private readonly HttpContent? _content;
        private readonly List<KeyValuePair<string, string[]>> _headers;
        private readonly List<KeyValuePair<string, object?>> _options;

        /// <summary>
        /// Captures what the caller set on <paramref name="request"/>. Call before the request is sent.
        /// </summary>
        /// <param name="request">The unsent request.</param>
        public RequestSnapshot(HttpRequestMessage request)
        {
            _method = request.Method;
            _requestUri = request.RequestUri;
            _version = request.Version;
            _versionPolicy = request.VersionPolicy;
            _content = request.Content;

            _headers = new List<KeyValuePair<string, string[]>>();
            foreach (KeyValuePair<string, HeaderStringValues> header in request.Headers.NonValidated)
            {
                _headers.Add(new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray()));
            }

            _options = new List<KeyValuePair<string, object?>>(request.Options);
        }

        /// <summary>
        /// Builds a new, unsent request carrying what was captured and sharing the original's content object.
        /// </summary>
        /// <returns>The request to send.</returns>
        public HttpRequestMessage CreateCopy()
        {
            HttpRequestMessage copy = new HttpRequestMessage(_method, _requestUri)
            {
                Version = _version,
                VersionPolicy = _versionPolicy,
                Content = _content,
            };

            foreach (KeyValuePair<string, string[]> header in _headers)
            {
                copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            IDictionary<string, object?> copyOptions = copy.Options;
            foreach (KeyValuePair<string, object?> option in _options)
            {
                copyOptions[option.Key] = option.Value;
            }

            return copy;
        }
    }
}
