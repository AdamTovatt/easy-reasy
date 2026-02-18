namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents the result of a refresh token operation.
    /// Use the static factory methods <see cref="Succeeded"/> and <see cref="Failed"/> to create instances.
    /// </summary>
    public sealed class RefreshResult
    {
        /// <summary>
        /// Whether the refresh operation was successful.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The authentication response containing the new access token and refresh token.
        /// Only populated when <see cref="Success"/> is true.
        /// </summary>
        public AuthResponse? AuthResponse { get; }

        /// <summary>
        /// The new raw refresh token issued as part of token rotation.
        /// Only populated when <see cref="Success"/> is true.
        /// </summary>
        public string? NewRefreshToken { get; }

        /// <summary>
        /// The reason the refresh operation failed.
        /// Only populated when <see cref="Success"/> is false.
        /// </summary>
        public RefreshFailureReason? FailureReason { get; }

        private RefreshResult(bool success, AuthResponse? authResponse, string? newRefreshToken, RefreshFailureReason? failureReason)
        {
            Success = success;
            AuthResponse = authResponse;
            NewRefreshToken = newRefreshToken;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Creates a successful refresh result with a new token pair.
        /// </summary>
        /// <param name="authResponse">The authentication response containing the new access token and refresh token.</param>
        /// <param name="newRefreshToken">The new raw refresh token.</param>
        /// <returns>A successful <see cref="RefreshResult"/>.</returns>
        public static RefreshResult Succeeded(AuthResponse authResponse, string newRefreshToken)
        {
            return new RefreshResult(true, authResponse, newRefreshToken, null);
        }

        /// <summary>
        /// Creates a failed refresh result with a failure reason.
        /// </summary>
        /// <param name="reason">The reason the refresh operation failed.</param>
        /// <returns>A failed <see cref="RefreshResult"/>.</returns>
        public static RefreshResult Failed(RefreshFailureReason reason)
        {
            return new RefreshResult(false, null, null, reason);
        }
    }
}
