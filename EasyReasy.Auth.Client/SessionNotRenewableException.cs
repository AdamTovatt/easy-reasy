namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Thrown when a client constructed from an <see cref="AuthResponse"/> needs a new token and has no way to get
    /// one: its token has expired, it holds no credentials, and it either has no refresh token left or the server
    /// refused the one it had.
    /// </summary>
    /// <remarks>
    /// The type is the whole answer: the saved session is spent, and a new client has to be constructed from
    /// credentials. It is not thrown when the refresh endpoint failed in a way worth retrying (<c>408</c>,
    /// <c>429</c> or a <c>5xx</c>); that surfaces as an <see cref="HttpRequestException"/> carrying the status, and
    /// the refresh token is kept for the next attempt. When the server refused the refresh token,
    /// <see cref="Exception.InnerException"/> is its answer, as an <see cref="HttpRequestException"/> with the
    /// status set. It derives from <see cref="InvalidOperationException"/>, so
    /// <c>catch (InvalidOperationException)</c> catches it.
    /// </remarks>
    public class SessionNotRenewableException : InvalidOperationException
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
