using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// A response feature whose response has already started, rejecting a status change the way a server
    /// does once headers are sent. <see cref="DefaultHttpContext"/>'s own feature never throws on its own.
    /// </summary>
    internal sealed class StartedHttpResponseFeature : IHttpResponseFeature
    {
        private int _statusCode = StatusCodes.Status200OK;

        public int StatusCode
        {
            get => _statusCode;
            set => throw new InvalidOperationException("StatusCode cannot be set because the response has already started.");
        }

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
