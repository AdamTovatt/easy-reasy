using System.Net;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Thrown when an auth endpoint answers with a success status but a body that is not an <see cref="AuthResponse"/>.
    /// </summary>
    /// <remarks>
    /// The usual cause is a base address that is not an auth server at all. A host that answers the auth path with a
    /// redirect to its own sign-in page returns <c>200</c> carrying HTML, and that is a success by every check the
    /// client can make, so the body reaches the parser and fails there. Reported as a parse failure alone, the two
    /// cases are indistinguishable: a wrong host and a genuinely corrupt response from the right one read the same.
    /// The properties here carry what separates them — above all <see cref="RequestUri"/>, which is the address that
    /// actually produced the body.
    /// </remarks>
    public class InvalidAuthResponseException : Exception
    {
        /// <summary>
        /// The maximum number of characters of the response body carried in <see cref="BodySnippet"/>.
        /// </summary>
        /// <remarks>
        /// Deliberately not a constant: a constant is copied into the assemblies that read it, so a later change to
        /// the cap would leave already-built code comparing against the old one.
        /// </remarks>
        public static readonly int BodySnippetLength = ResponseBodySnippet.MaximumLength;

        /// <summary>
        /// The URI the request ended at, after any redirects the handler followed, or <c>null</c> when the handler
        /// recorded none. This — not the client's base address — is the address that answered: once a redirect has
        /// been followed the two are different hosts or paths, and this is the one a caller has to be told about.
        /// </summary>
        public Uri? RequestUri { get; }

        /// <summary>
        /// The status the endpoint answered with. Always a success status: an unsuccessful one is reported by the
        /// client before the body is ever parsed.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// The response's content type, or <c>null</c> when it carried none. <c>text/html</c> here is the signature
        /// of a sign-in page answering where an auth endpoint was expected.
        /// </summary>
        public string? ContentType { get; }

        /// <summary>
        /// The start of the response body, at most <see cref="BodySnippetLength"/> characters, with runs of
        /// whitespace collapsed to single spaces so that a message stays readable, and a trailing <c>…</c> when the
        /// body continued past the cap. Empty when the body was empty or held nothing but whitespace.
        /// </summary>
        /// <remarks>
        /// Values under a name that reads as a secret — anything containing <c>token</c>, <c>secret</c> or
        /// <c>password</c>, whether it is a JSON property or an HTML field — are replaced by <c>[REDACTED]</c>
        /// first, by the same rule <see cref="AuthResponse.ToString"/> is blanked by. A body that nearly is an
        /// auth response carries a real token, and a sign-in page carries the hidden fields of one; this snippet is
        /// written to exactly the logs that redaction exists for.
        /// </remarks>
        public string BodySnippet { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidAuthResponseException"/> class.
        /// </summary>
        /// <remarks>
        /// Internal because <see cref="BodySnippet"/> is documented as redacted, collapsed and capped, and
        /// <see cref="FromResponse"/> is what makes it so. A constructor that took any string could not hold that
        /// up, and an unredacted body reaching a property whose contract says it is redacted is the one mistake
        /// this type exists to prevent.
        /// </remarks>
        /// <param name="message">A description of what the endpoint answered with.</param>
        /// <param name="requestUri">The URI the request ended at after any redirects, or <c>null</c> when none was recorded.</param>
        /// <param name="statusCode">The status the endpoint answered with.</param>
        /// <param name="contentType">The response's content type, or <c>null</c> when it carried none.</param>
        /// <param name="bodySnippet">The start of the response body, as described on <see cref="BodySnippet"/>.</param>
        /// <param name="innerException">The failure that surfaced the body as unreadable, or <c>null</c> when there was none.</param>
        internal InvalidAuthResponseException(
            string message,
            Uri? requestUri,
            HttpStatusCode statusCode,
            string? contentType,
            string bodySnippet,
            Exception? innerException = null)
            : base(message, innerException)
        {
            RequestUri = requestUri;
            StatusCode = statusCode;
            ContentType = contentType;
            BodySnippet = bodySnippet;
        }

        /// <summary>
        /// Creates the exception for a response whose body was not an <see cref="AuthResponse"/>. Every place that
        /// parses an auth endpoint's body routes its failure through here, so a wrong host reports the same way
        /// whichever request reached it first.
        /// </summary>
        /// <param name="response">The response whose body could not be read as an <see cref="AuthResponse"/>.</param>
        /// <param name="body">The body as it was read, before redacting and capping.</param>
        /// <param name="innerException">The failure that surfaced the body as unreadable.</param>
        /// <returns>An exception carrying the endpoint, status, content type and the start of the body.</returns>
        internal static InvalidAuthResponseException FromResponse(HttpResponseMessage response, string body, Exception innerException)
        {
            Uri? requestUri = response.RequestMessage?.RequestUri;
            string? contentType = response.Content.Headers.ContentType?.ToString();
            string bodySnippet = ResponseBodySnippet.Create(body);

            string endpointPart = requestUri == null ? "The auth endpoint" : $"The auth endpoint at {requestUri}";
            string contentTypePart = contentType == null ? "no content type" : $"content type {contentType}";
            string bodyPart = bodySnippet.Length == 0 ? "The body was empty." : $"Body: {bodySnippet}";

            string message =
                $"{endpointPart} answered {(int)response.StatusCode} with {contentTypePart}, " +
                $"and the body is not an {nameof(AuthResponse)}. " +
                $"An address that is not an auth server answers exactly this way — the URI named above is the one " +
                $"the request ended at, after any redirects, so it is the address that produced this body. " +
                bodyPart;

            return new InvalidAuthResponseException(message, requestUri, response.StatusCode, contentType, bodySnippet, innerException);
        }
    }
}
