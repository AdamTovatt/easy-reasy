using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What <see cref="IOAuthGrantHandler.ApproveAsync"/> decided. Use <see cref="Approve"/> or <see cref="Refuse"/>.
    /// </summary>
    public sealed class OAuthApprovalDecision
    {
        private OAuthApprovalDecision(OAuthGrant? grant, string? refusalDetail)
        {
            Grant = grant;
            RefusalDetail = refusalDetail;
        }

        /// <summary>
        /// The grant, when approved; null when refused.
        /// </summary>
        public OAuthGrant? Grant { get; }

        /// <summary>
        /// Why the approval was refused, for the consent page and the audit record, or null.
        /// </summary>
        public string? RefusalDetail { get; }

        /// <summary>
        /// Whether the approval was refused.
        /// </summary>
        [MemberNotNullWhen(false, nameof(Grant))]
        public bool IsRefused => Grant == null;

        /// <summary>
        /// Approves the request: the server issues an authorization code carrying <paramref name="grant"/> and returns the
        /// client's redirect.
        /// </summary>
        /// <param name="grant">What the approval grants.</param>
        /// <returns>The decision.</returns>
        public static OAuthApprovalDecision Approve(OAuthGrant grant)
        {
            ArgumentNullException.ThrowIfNull(grant);
            ArgumentException.ThrowIfNullOrWhiteSpace(grant.GrantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(grant.Subject);
            ArgumentException.ThrowIfNullOrWhiteSpace(grant.AuthType);
            ArgumentNullException.ThrowIfNull(grant.Claims);
            ArgumentNullException.ThrowIfNull(grant.Roles);
            return new OAuthApprovalDecision(grant, null);
        }

        /// <summary>
        /// Refuses this approval: the approve endpoint answers 403 with <paramref name="detail"/>, and the request stays
        /// pending, so the person can choose differently (another organization, say) or deny it.
        /// </summary>
        /// <param name="detail">Why, in words the consent page can show, or null.</param>
        /// <returns>The decision.</returns>
        public static OAuthApprovalDecision Refuse(string? detail = null)
        {
            return new OAuthApprovalDecision(null, detail);
        }
    }
}
