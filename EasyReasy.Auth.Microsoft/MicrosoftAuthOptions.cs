namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Configuration for Microsoft sign-in: the app registration's credentials, the key that protects the
    /// OAuth <c>state</c>, the redirect URIs flows may land on, and the tenants accepted.
    /// </summary>
    /// <remarks>
    /// Validated as a whole by <c>AddEasyReasyMicrosoftAuth</c>, which reports every problem in one exception.
    /// </remarks>
    public class MicrosoftAuthOptions
    {
        /// <summary>
        /// The default <see cref="AuthorityBaseUrl"/>: Microsoft Entra ID's global cloud.
        /// </summary>
        public const string DefaultAuthorityBaseUrl = "https://login.microsoftonline.com";

        /// <summary>
        /// The app registration's application (client) id. An ID token's audience must equal it.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// A client secret of the app registration, sent to Microsoft's token endpoint when the authorization
        /// code is redeemed. It never reaches the browser.
        /// </summary>
        public required string ClientSecret { get; init; }

        /// <summary>
        /// The 32-byte AES-256 key that encrypts and authenticates the OAuth <c>state</c>, so only this server can
        /// read or forge it. Give it its own key: never the key of a cipher the application registers for another
        /// purpose, such as encrypting MFA secrets. The key is copied at registration.
        /// </summary>
        public required byte[] StateKey { get; init; }

        /// <summary>
        /// The redirect URIs a flow may name, each registered on the app registration's Web platform. Each flow
        /// (sign-in, linking an account, a re-check at signing) lands on its own page of the application, which
        /// knows from its own route where to post the <c>code</c> and <c>state</c>. At least one is required.
        /// Matching is exact, as Microsoft's is.
        /// </summary>
        public required IReadOnlyList<string> RedirectUris { get; init; }

        /// <summary>
        /// Tenant ids whose accounts are accepted. Null or empty accepts any work or school tenant; personal Microsoft
        /// accounts are refused either way, and the personal accounts tenant may not be listed.
        /// </summary>
        public IReadOnlyCollection<Guid>? AllowedTenantIds { get; init; }

        /// <summary>
        /// The base URL of the Microsoft identity platform, without a tenant segment. Defaults to
        /// <see cref="DefaultAuthorityBaseUrl"/>. Set it to another cloud's login host, or to a local fake identity
        /// provider in tests. It must be https, except on a loopback host.
        /// </summary>
        public string AuthorityBaseUrl { get; init; } = DefaultAuthorityBaseUrl;

        /// <summary>
        /// How long a begun flow stays completable: the lifetime of the <c>state</c> and of the browser binding
        /// cookie. Defaults to 10 minutes.
        /// </summary>
        public TimeSpan StateLifetime { get; init; } = TimeSpan.FromMinutes(10);
    }
}
