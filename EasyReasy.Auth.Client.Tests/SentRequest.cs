namespace EasyReasy.Auth.Client.Tests
{
    /// <summary>
    /// A request that reached <see cref="FakeHttpHandler"/>, with the body it carried when it was sent.
    /// </summary>
    public class SentRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SentRequest"/> class.
        /// </summary>
        /// <param name="message">The request message.</param>
        /// <param name="body">The body as UTF-8 text, read while the request was sent, or <c>null</c> for none.</param>
        public SentRequest(HttpRequestMessage message, string? body)
        {
            Message = message;
            Body = body;
        }

        /// <summary>
        /// Gets the request message.
        /// </summary>
        public HttpRequestMessage Message { get; }

        /// <summary>
        /// Gets the body as UTF-8 text, read while the request was sent, or <c>null</c> for a request without
        /// content. Read then because a body the handler reads later may no longer be the one that was sent.
        /// </summary>
        public string? Body { get; }
    }
}
