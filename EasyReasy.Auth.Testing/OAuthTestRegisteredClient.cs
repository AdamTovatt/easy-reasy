namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// A client as <see cref="OAuthTestClient"/> drives it: its id, its secret when confidential, and the redirect URI its
    /// flows use. <see cref="OAuthTestClient.RegisterAsync"/> returns one; a test builds one itself for a client identified
    /// by a metadata document URL, or one it stored directly. A record, so a test changes one value with <c>with</c>.
    /// </summary>
    public sealed record OAuthTestRegisteredClient
    {
        /// <summary>
        /// The client id.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// The client secret, for a confidential client; null for a public one.
        /// </summary>
        public string? ClientSecret { get; init; }

        /// <summary>
        /// How the client authenticates at the token and revocation endpoints.
        /// </summary>
        public required OAuthClientAuthenticationMethod AuthenticationMethod { get; init; }

        /// <summary>
        /// The redirect URI the client's authorization requests name.
        /// </summary>
        public required string RedirectUri { get; init; }
    }
}
