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

        /// <summary>
        /// The subject identifier (user id) associated with the refresh token.
        /// Populated on success and on all failure reasons except <see cref="RefreshFailureReason.TokenNotFound"/>,
        /// which has no associated stored token.
        /// Intended to support security audit logging at the consumer site.
        /// </summary>
        public string? Subject { get; }

        /// <summary>
        /// The family identifier linking a chain of refresh tokens created by rotation.
        /// Populated on success and on all failure reasons except <see cref="RefreshFailureReason.TokenNotFound"/>,
        /// which has no associated stored token.
        /// Intended to support security audit logging at the consumer site.
        /// </summary>
        public string? FamilyId { get; }

        private RefreshResult(
            bool success,
            AuthResponse? authResponse,
            string? newRefreshToken,
            RefreshFailureReason? failureReason,
            string? subject,
            string? familyId)
        {
            Success = success;
            AuthResponse = authResponse;
            NewRefreshToken = newRefreshToken;
            FailureReason = failureReason;
            Subject = subject;
            FamilyId = familyId;
        }

        /// <summary>
        /// Creates a successful refresh result with a new token pair.
        /// </summary>
        /// <param name="authResponse">The authentication response containing the new access token and refresh token.</param>
        /// <param name="newRefreshToken">The new raw refresh token.</param>
        /// <param name="subject">The subject identifier (user id) associated with the refresh token. Optional; defaults to <c>null</c>.</param>
        /// <param name="familyId">The family identifier linking rotated refresh tokens. Optional; defaults to <c>null</c>.</param>
        /// <returns>A successful <see cref="RefreshResult"/>.</returns>
        public static RefreshResult Succeeded(
            AuthResponse authResponse,
            string newRefreshToken,
            string? subject = null,
            string? familyId = null)
        {
            return new RefreshResult(true, authResponse, newRefreshToken, null, subject, familyId);
        }

        /// <summary>
        /// Creates a failed refresh result with a failure reason.
        /// </summary>
        /// <param name="reason">The reason the refresh operation failed.</param>
        /// <param name="subject">The subject identifier (user id) associated with the refresh token, when available. Optional; defaults to <c>null</c>.</param>
        /// <param name="familyId">The family identifier linking rotated refresh tokens, when available. Optional; defaults to <c>null</c>.</param>
        /// <returns>A failed <see cref="RefreshResult"/>.</returns>
        public static RefreshResult Failed(
            RefreshFailureReason reason,
            string? subject = null,
            string? familyId = null)
        {
            return new RefreshResult(false, null, null, reason, subject, familyId);
        }
    }
}
