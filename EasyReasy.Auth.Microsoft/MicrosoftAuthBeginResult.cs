namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The output of <see cref="IMicrosoftAuthService.Begin"/>: where to send the browser, and the <c>state</c> it carries.
    /// </summary>
    public sealed class MicrosoftAuthBeginResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftAuthBeginResult"/> class.
        /// </summary>
        /// <param name="authorizationUrl">Microsoft's authorize URL with every parameter of the flow.</param>
        /// <param name="state">The encrypted <c>state</c>, also present in <paramref name="authorizationUrl"/>.</param>
        public MicrosoftAuthBeginResult(string authorizationUrl, string state)
        {
            AuthorizationUrl = authorizationUrl;
            State = state;
        }

        /// <summary>
        /// Microsoft's authorize URL with every parameter of the flow. Send the browser here with a top-level
        /// navigation.
        /// </summary>
        public string AuthorizationUrl { get; }

        /// <summary>
        /// The encrypted <c>state</c>, also present in <see cref="AuthorizationUrl"/>. Microsoft hands it back on the
        /// redirect, and it goes to Complete with the code.
        /// </summary>
        public string State { get; }
    }
}
