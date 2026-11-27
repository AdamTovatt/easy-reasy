namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The output of <see cref="ISamlAuthService.BeginAsync"/>: where to send the browser.
    /// </summary>
    public sealed class SamlAuthBeginResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamlAuthBeginResult"/> class.
        /// </summary>
        /// <param name="redirectUrl">The identity provider's single sign-on URL carrying the AuthnRequest.</param>
        public SamlAuthBeginResult(string redirectUrl)
        {
            RedirectUrl = redirectUrl;
        }

        /// <summary>
        /// The identity provider's single sign-on URL with the deflated AuthnRequest (and its signature, when the service
        /// provider signs requests) in the query string. Send the browser here with a top-level navigation.
        /// </summary>
        public string RedirectUrl { get; }
    }
}
