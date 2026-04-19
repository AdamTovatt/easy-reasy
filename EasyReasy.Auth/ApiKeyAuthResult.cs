namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the result of an API key authentication validation.
    /// Use the static factory methods <see cref="Succeeded"/> and <see cref="Failed"/> to create instances.
    /// </summary>
    /// <remarks>
    /// Deliberately does not carry the raw API key, so that consumer code logging the whole result
    /// cannot accidentally leak secrets.
    /// </remarks>
    public sealed class ApiKeyAuthResult
    {
        /// <summary>
        /// Whether the API key authentication attempt succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The authentication response containing the access token and (optionally) a refresh token.
        /// Only populated when <see cref="Success"/> is true.
        /// </summary>
        public AuthResponse? AuthResponse { get; }

        /// <summary>
        /// The client identifier associated with the attempt.
        /// Populated on success, and on failure when the client identifier is known — even if the key is unknown
        /// (<see cref="ApiKeyAuthFailureReason.UnknownKey"/>). Intended to support ISO 27001 A.12.4.1 audit logging.
        /// </summary>
        public string? AttemptedClientId { get; }

        /// <summary>
        /// The reason the API key authentication attempt failed.
        /// Only populated when <see cref="Success"/> is false.
        /// </summary>
        public ApiKeyAuthFailureReason? FailureReason { get; }

        private ApiKeyAuthResult(
            bool success,
            AuthResponse? authResponse,
            string? attemptedClientId,
            ApiKeyAuthFailureReason? failureReason)
        {
            Success = success;
            AuthResponse = authResponse;
            AttemptedClientId = attemptedClientId;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Creates a successful API key authentication result.
        /// </summary>
        /// <param name="authResponse">The authentication response containing the access token.</param>
        /// <param name="clientId">The client identifier that was authenticated.</param>
        /// <returns>A successful <see cref="ApiKeyAuthResult"/>.</returns>
        public static ApiKeyAuthResult Succeeded(AuthResponse authResponse, string clientId)
        {
            return new ApiKeyAuthResult(true, authResponse, clientId, null);
        }

        /// <summary>
        /// Creates a failed API key authentication result.
        /// </summary>
        /// <param name="reason">The reason the API key authentication attempt failed.</param>
        /// <param name="attemptedClientId">
        /// The client identifier that was attempted, when available. May be <c>null</c> if the request did not
        /// include one. Populate whenever possible so audit logs can attribute the failure.
        /// </param>
        /// <returns>A failed <see cref="ApiKeyAuthResult"/>.</returns>
        public static ApiKeyAuthResult Failed(ApiKeyAuthFailureReason reason, string? attemptedClientId = null)
        {
            return new ApiKeyAuthResult(false, null, attemptedClientId, reason);
        }
    }
}
