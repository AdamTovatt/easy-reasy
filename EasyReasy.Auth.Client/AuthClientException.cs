namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// The base of the exceptions <see cref="AuthorizedHttpClient"/> throws for an authorization outcome that
    /// retrying the same request will not change: the credentials refused, the session spent, or an address that
    /// is not an auth server.
    /// </summary>
    /// <remarks>
    /// Catch it to handle every such outcome in one place. A server that failed to answer — a status that is not a
    /// refusal as <see cref="SignInRefusedException"/> defines one, or no answer at all — is not one of these: that
    /// surfaces as <see cref="HttpRequestException"/>, because retrying later may succeed.
    /// </remarks>
    public abstract class AuthClientException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthClientException"/> class.
        /// </summary>
        protected AuthClientException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthClientException"/> class with a message.
        /// </summary>
        /// <param name="message">A description of how authorization failed.</param>
        protected AuthClientException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthClientException"/> class with a message and the failure
        /// that caused it.
        /// </summary>
        /// <param name="message">A description of how authorization failed.</param>
        /// <param name="innerException">The failure that caused this one, or <c>null</c> when there was none.</param>
        protected AuthClientException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
