namespace EasyReasy.Auth
{
    /// <summary>
    /// A client recorded in <see cref="IOAuthServerStore"/>. Dynamic client registration creates one; an application can
    /// also store one itself for a client it sets up by hand. Recording a client grants it nothing: a connection exists
    /// only once a person approves it through <see cref="IOAuthGrantHandler.ApproveAsync"/>.
    /// </summary>
    public sealed class OAuthClient
    {
        /// <summary>
        /// The client identifier, as the client sends it. The primary lookup key.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// The SHA-256 hash (lowercase hex) of the client secret, for a confidential client; null for a public client
        /// (<see cref="OAuthClientAuthenticationMethod.None"/>). The raw secret is shown to the client once, at
        /// registration, and never stored.
        /// </summary>
        public string? ClientSecretHash { get; init; }

        /// <summary>
        /// How the client authenticates at the token and revocation endpoints.
        /// </summary>
        public required OAuthClientAuthenticationMethod AuthenticationMethod { get; init; }

        /// <summary>
        /// The redirect URIs the client registered. An authorization request's <c>redirect_uri</c> must equal one of them
        /// exactly, except that the port of an http loopback URI may differ (RFC 8252 section 7.3).
        /// </summary>
        public required IReadOnlyList<string> RedirectUris { get; init; }

        /// <summary>
        /// The name the client registered (<c>client_name</c>), or null. Self-asserted: show it beside who approved the
        /// connection, never as proof of who the client is.
        /// </summary>
        public string? ClientName { get; init; }

        /// <summary>
        /// The home page the client registered (<c>client_uri</c>), or null. Self-asserted, like the name.
        /// </summary>
        public string? ClientUri { get; init; }

        /// <summary>
        /// The UTC time the client was recorded.
        /// </summary>
        public required DateTime CreatedAt { get; init; }
    }
}
