using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of a dynamic client registration, reported to <see cref="IAuthAuditLogger.OnOAuthClientRegistrationAsync"/>.
    /// It never carries the client secret. Use the static factory methods <see cref="Succeeded"/> and <see cref="Failed"/>
    /// to create instances.
    /// </summary>
    public sealed class OAuthClientRegistrationResult
    {
        private OAuthClientRegistrationResult(
            string? clientId,
            string? clientName,
            IReadOnlyList<string>? redirectUris,
            OAuthClientAuthenticationMethod? authenticationMethod,
            string? failureCode)
        {
            ClientId = clientId;
            ClientName = clientName;
            RedirectUris = redirectUris;
            AuthenticationMethod = authenticationMethod;
            FailureCode = failureCode;
        }

        /// <summary>
        /// Whether the client was recorded.
        /// </summary>
        [MemberNotNullWhen(true, nameof(ClientId), nameof(RedirectUris), nameof(AuthenticationMethod))]
        [MemberNotNullWhen(false, nameof(FailureCode))]
        public bool Success => FailureCode == null;

        /// <summary>
        /// The new client's id, on success.
        /// </summary>
        public string? ClientId { get; }

        /// <summary>
        /// The self-asserted name, when the registration gave a readable one.
        /// </summary>
        public string? ClientName { get; }

        /// <summary>
        /// The redirect URIs, on success.
        /// </summary>
        public IReadOnlyList<string>? RedirectUris { get; }

        /// <summary>
        /// The authentication method registered, on success.
        /// </summary>
        public OAuthClientAuthenticationMethod? AuthenticationMethod { get; }

        /// <summary>
        /// The RFC 7591 error code the registration was refused with (<c>invalid_redirect_uri</c>,
        /// <c>invalid_client_metadata</c>), or null on success.
        /// </summary>
        public string? FailureCode { get; }

        /// <summary>
        /// Creates the result of a recorded registration.
        /// </summary>
        /// <param name="clientId">The new client's id.</param>
        /// <param name="clientName">The self-asserted name, or null.</param>
        /// <param name="redirectUris">The redirect URIs.</param>
        /// <param name="authenticationMethod">The authentication method registered.</param>
        /// <returns>A successful result.</returns>
        public static OAuthClientRegistrationResult Succeeded(
            string clientId,
            string? clientName,
            IReadOnlyList<string> redirectUris,
            OAuthClientAuthenticationMethod authenticationMethod)
        {
            return new OAuthClientRegistrationResult(clientId, clientName, redirectUris, authenticationMethod, null);
        }

        /// <summary>
        /// Creates the result of a refused registration.
        /// </summary>
        /// <param name="failureCode">The RFC 7591 error code it was refused with.</param>
        /// <returns>A failed result.</returns>
        public static OAuthClientRegistrationResult Failed(string failureCode)
        {
            return new OAuthClientRegistrationResult(null, null, null, null, failureCode);
        }
    }
}
