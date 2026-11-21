namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of a dynamic client registration, reported to <see cref="IAuthAuditLogger.OnOAuthClientRegistrationAsync"/>.
    /// It never carries the client secret.
    /// </summary>
    public sealed class OAuthClientRegistrationResult
    {
        /// <summary>
        /// Whether the client was recorded.
        /// </summary>
        public required bool Success { get; init; }

        /// <summary>
        /// The new client's id, on success.
        /// </summary>
        public string? ClientId { get; init; }

        /// <summary>
        /// The self-asserted name, when the registration gave a readable one.
        /// </summary>
        public string? ClientName { get; init; }

        /// <summary>
        /// The redirect URIs, on success.
        /// </summary>
        public IReadOnlyList<string>? RedirectUris { get; init; }

        /// <summary>
        /// The authentication method registered, on success.
        /// </summary>
        public OAuthClientAuthenticationMethod? AuthenticationMethod { get; init; }

        /// <summary>
        /// The RFC 7591 error code the registration was refused with (<c>invalid_redirect_uri</c>,
        /// <c>invalid_client_metadata</c>), or null on success.
        /// </summary>
        public string? FailureCode { get; init; }
    }
}
