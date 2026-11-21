using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of a token request, reported to <see cref="IAuthAuditLogger.OnOAuthTokenAsync"/>. It never carries a
    /// code or token. Use the static factory methods <see cref="Succeeded"/> and <see cref="Failed"/> to create instances.
    /// </summary>
    public sealed class OAuthTokenResult
    {
        private OAuthTokenResult(
            OAuthGrantType? grantType,
            string? clientId,
            string? grantId,
            string? subject,
            string? familyId,
            OAuthTokenFailureReason? failureReason)
        {
            GrantType = grantType;
            ClientId = clientId;
            GrantId = grantId;
            Subject = subject;
            FamilyId = familyId;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Whether tokens were issued.
        /// </summary>
        [MemberNotNullWhen(true, nameof(GrantType), nameof(ClientId), nameof(GrantId))]
        [MemberNotNullWhen(false, nameof(FailureReason))]
        public bool Success => FailureReason == null;

        /// <summary>
        /// The grant type, or null when the request named none the server supports.
        /// </summary>
        public OAuthGrantType? GrantType { get; }

        /// <summary>
        /// The client id the request named, authenticated or not; null when it named none. Check
        /// <see cref="FailureReason"/> before trusting it: on <see cref="OAuthTokenFailureReason.InvalidClient"/> it is
        /// only what the caller claimed.
        /// </summary>
        public string? ClientId { get; }

        /// <summary>
        /// The grant the code or refresh token belongs to, when one was found.
        /// </summary>
        public string? GrantId { get; }

        /// <summary>
        /// The subject the tokens are for, when a code or refresh token was found.
        /// </summary>
        public string? Subject { get; }

        /// <summary>
        /// The refresh token family, when a code or refresh token was found.
        /// </summary>
        public string? FamilyId { get; }

        /// <summary>
        /// Why the request was refused, or null on success.
        /// </summary>
        public OAuthTokenFailureReason? FailureReason { get; }

        /// <summary>
        /// Creates the result of a request that issued tokens.
        /// </summary>
        /// <param name="grantType">The grant type.</param>
        /// <param name="clientId">The authenticated client.</param>
        /// <param name="grantId">The grant the tokens were issued under.</param>
        /// <param name="subject">The subject the tokens are for.</param>
        /// <param name="familyId">The refresh token family.</param>
        /// <returns>A successful result.</returns>
        public static OAuthTokenResult Succeeded(OAuthGrantType grantType, string clientId, string grantId, string? subject, string? familyId)
        {
            return new OAuthTokenResult(grantType, clientId, grantId, subject, familyId, null);
        }

        /// <summary>
        /// Creates the result of a refused request.
        /// </summary>
        /// <param name="reason">Why it was refused.</param>
        /// <param name="grantType">The grant type, or null when the request named none the server supports.</param>
        /// <param name="clientId">The client id the request named, or null when it named none.</param>
        /// <param name="grantId">The grant the code or refresh token belongs to, when one was found.</param>
        /// <param name="subject">The subject, when a code or refresh token was found.</param>
        /// <param name="familyId">The refresh token family, when a code or refresh token was found.</param>
        /// <returns>A failed result.</returns>
        public static OAuthTokenResult Failed(
            OAuthTokenFailureReason reason,
            OAuthGrantType? grantType,
            string? clientId,
            string? grantId = null,
            string? subject = null,
            string? familyId = null)
        {
            return new OAuthTokenResult(grantType, clientId, grantId, subject, familyId, reason);
        }
    }
}
