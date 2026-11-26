namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The output of a flow's Begin: where to send the browser, and the <c>state</c> it carries.
    /// </summary>
    public sealed class OpenIdConnectAuthBeginResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectAuthBeginResult"/> class.
        /// </summary>
        /// <param name="authorizationUrl">The provider's authorize URL with every parameter of the flow.</param>
        /// <param name="state">The encrypted <c>state</c>, also present in <paramref name="authorizationUrl"/>.</param>
        public OpenIdConnectAuthBeginResult(string authorizationUrl, string state)
        {
            AuthorizationUrl = authorizationUrl;
            State = state;
        }

        /// <summary>
        /// The provider's authorize URL with every parameter of the flow. Send the browser here with a top-level
        /// navigation.
        /// </summary>
        public string AuthorizationUrl { get; }

        /// <summary>
        /// The encrypted <c>state</c>, also present in <see cref="AuthorizationUrl"/>. The provider hands it back on the
        /// redirect, and it goes to completion with the code.
        /// </summary>
        public string State { get; }
    }
}
