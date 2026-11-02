namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Thrown when a client constructed from an <see cref="AuthResponse"/> needs a new token and has no way to get
    /// one: its token has expired, it holds no credentials, and it either has no refresh token left or the server
    /// refused the one it had.
    /// </summary>
    /// <remarks>
    /// The type is the whole answer: the saved session is spent, and a new client has to be constructed from
    /// credentials. When the server refused the refresh token, <see cref="Exception.InnerException"/> is its
    /// answer, as an <see cref="HttpRequestException"/> with the status set; a <c>404</c> there points at a
    /// misconfigured refresh endpoint rather than a spent session. It is not thrown when the refresh endpoint
    /// failed rather than refused — a status that is not a refusal as <see cref="SignInRefusedException"/>
    /// defines one, or no answer. That surfaces as an <see cref="HttpRequestException"/>, and the refresh token is
    /// kept for the next attempt.
    /// </remarks>
    public class SessionNotRenewableException : AuthClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SessionNotRenewableException"/> class.
        /// </summary>
        public SessionNotRenewableException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionNotRenewableException"/> class with a message.
        /// </summary>
        /// <param name="message">A description of why the session cannot be renewed.</param>
        public SessionNotRenewableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionNotRenewableException"/> class with a message and
        /// the failure that caused it.
        /// </summary>
        /// <param name="message">A description of why the session cannot be renewed.</param>
        /// <param name="innerException">The failure that caused this one, or <c>null</c> when there was none.</param>
        public SessionNotRenewableException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
