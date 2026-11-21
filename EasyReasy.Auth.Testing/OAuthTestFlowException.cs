using System.Net;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// Thrown by <see cref="OAuthTestClient"/> when the server answers a step other than the way a successful flow is
    /// answered: an OAuth error, a refusal from the app's grant handler, or a redirect where none was expected. A test of a
    /// refusal asserts on <see cref="Error"/> and <see cref="StatusCode"/>.
    /// </summary>
    public sealed class OAuthTestFlowException : Exception
    {
        /// <summary>
        /// Initializes the exception.
        /// </summary>
        /// <param name="message">What went differently.</param>
        /// <param name="statusCode">The response's status.</param>
        /// <param name="error">The OAuth <c>error</c> code, from the body or the redirect, when there was one.</param>
        /// <param name="errorDescription">The <c>error_description</c>, when there was one.</param>
        /// <param name="body">The response body, possibly empty.</param>
        public OAuthTestFlowException(string message, HttpStatusCode statusCode, string? error, string? errorDescription, string body)
            : base(message)
        {
            StatusCode = statusCode;
            Error = error;
            ErrorDescription = errorDescription;
            Body = body;
        }

        /// <summary>
        /// The response's status.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// The OAuth <c>error</c> code, such as <c>invalid_grant</c>, when the server gave one.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// The <c>error_description</c>, when the server gave one.
        /// </summary>
        public string? ErrorDescription { get; }

        /// <summary>
        /// The response body, possibly empty.
        /// </summary>
        public string Body { get; }
    }
}
