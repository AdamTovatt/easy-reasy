namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Exception thrown when a Google ID token fails validation.
    /// This includes invalid signatures, expired tokens, audience mismatches,
    /// and hosted domain rejections.
    /// </summary>
    public class GoogleTokenValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleTokenValidationException"/> class.
        /// </summary>
        /// <param name="message">The message describing the validation failure.</param>
        public GoogleTokenValidationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleTokenValidationException"/> class.
        /// </summary>
        /// <param name="message">The message describing the validation failure.</param>
        /// <param name="innerException">The inner exception that caused the validation failure.</param>
        public GoogleTokenValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
