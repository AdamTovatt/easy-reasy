using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A client's form post to the token or revocation endpoint, read and with its client authenticated: either a form
    /// whose client authenticated or not, or an <c>invalid_request</c> for a body that could not be read as one.
    /// </summary>
    internal sealed class OAuthClientRequest
    {
        private OAuthClientRequest(OAuthParameters? form, OAuthClientAuthentication? client, OAuthError? requestError)
        {
            Form = form;
            Client = client;
            RequestError = requestError;
        }

        /// <summary>
        /// Whether the body was a form with no parameter repeated. When it was, <see cref="Client"/> says whether the
        /// client authenticated.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Form), nameof(Client))]
        [MemberNotNullWhen(false, nameof(RequestError))]
        public bool IsWellFormed => RequestError == null;

        /// <summary>
        /// The form parameters, when the body was well formed.
        /// </summary>
        public OAuthParameters? Form { get; }

        /// <summary>
        /// The client's authentication, succeeded or failed, when the body was well formed.
        /// </summary>
        public OAuthClientAuthentication? Client { get; }

        /// <summary>
        /// The <c>invalid_request</c> to answer, when the body was not well formed.
        /// </summary>
        public OAuthError? RequestError { get; }

        /// <summary>
        /// A well-formed request.
        /// </summary>
        /// <param name="form">The form parameters.</param>
        /// <param name="client">The client's authentication.</param>
        /// <returns>The request.</returns>
        public static OAuthClientRequest WellFormed(OAuthParameters form, OAuthClientAuthentication client)
        {
            return new OAuthClientRequest(form, client, null);
        }

        /// <summary>
        /// A body that could not be read as a form.
        /// </summary>
        /// <param name="description">Why, for the error description.</param>
        /// <returns>The request.</returns>
        public static OAuthClientRequest Malformed(string description)
        {
            return new OAuthClientRequest(null, null, new OAuthError(OAuthError.InvalidRequest, description));
        }
    }
}
