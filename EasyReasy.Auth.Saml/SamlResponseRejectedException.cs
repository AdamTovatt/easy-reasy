namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Thrown when a SAML response is refused. The message says which check failed; when the library refused the
    /// response, its exception is the inner exception.
    /// </summary>
    internal sealed class SamlResponseRejectedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamlResponseRejectedException"/> class.
        /// </summary>
        /// <param name="message">Which check failed.</param>
        public SamlResponseRejectedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlResponseRejectedException"/> class.
        /// </summary>
        /// <param name="message">Which check failed.</param>
        /// <param name="innerException">The library's exception.</param>
        public SamlResponseRejectedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
