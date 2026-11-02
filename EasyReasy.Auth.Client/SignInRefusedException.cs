using System.Net;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Thrown when the sign-in endpoint refuses the client's credentials. A refusal is any <c>4xx</c> other than
    /// <c>408 Request Timeout</c> and <c>429 Too Many Requests</c>, <c>401 Unauthorized</c> included; every other
    /// unsuccessful status is the server failing rather than refusing, and surfaces as
    /// <see cref="HttpRequestException"/> with its status set. The refresh endpoint is judged by the same rule,
    /// which <see cref="AuthorizedHttpClient.IsRefusalStatus"/> applies.
    /// </summary>
    /// <remarks>
    /// A refusal is the server's judgement of the credentials, so sending them again will be refused again; fix
    /// the credentials or the endpoint instead. <see cref="StatusCode"/> says which refusal it was.
    /// </remarks>
    public class SignInRefusedException : AuthClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SignInRefusedException"/> class.
        /// </summary>
        /// <param name="statusCode">The status the sign-in endpoint refused with.</param>
        public SignInRefusedException(HttpStatusCode statusCode)
            : base(DescribeRefusal(statusCode))
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignInRefusedException"/> class with a message.
        /// </summary>
        /// <param name="message">A description of the refusal.</param>
        /// <param name="statusCode">The status the sign-in endpoint refused with.</param>
        public SignInRefusedException(string message, HttpStatusCode statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignInRefusedException"/> class with a message and the
        /// failure that caused it.
        /// </summary>
        /// <param name="message">A description of the refusal.</param>
        /// <param name="statusCode">The status the sign-in endpoint refused with.</param>
        /// <param name="innerException">The failure that caused this one, or <c>null</c> when there was none.</param>
        public SignInRefusedException(string message, HttpStatusCode statusCode, Exception? innerException)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// The status the sign-in endpoint refused with.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Builds the exception for a sign-in endpoint that refused, quoting what it answered with.
        /// </summary>
        /// <param name="response">The refusing response.</param>
        /// <param name="bodySnippet">The response body, already capped and redacted by <see cref="ResponseBodySnippet"/>.</param>
        /// <returns>The exception to throw.</returns>
        internal static SignInRefusedException FromResponse(HttpResponseMessage response, string bodySnippet)
        {
            return new SignInRefusedException($"{DescribeRefusal(response.StatusCode)} Content: {bodySnippet}", response.StatusCode);
        }

        private static string DescribeRefusal(HttpStatusCode statusCode)
        {
            return $"The sign-in endpoint refused the credentials with {HttpStatusText.Describe(statusCode)}.";
        }
    }
}
