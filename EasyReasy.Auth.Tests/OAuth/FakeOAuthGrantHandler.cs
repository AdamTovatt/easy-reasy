using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// An <see cref="IOAuthGrantHandler"/> whose decisions a test sets, recording every context it was called with. By
    /// default it approves for the signed-in person under a new grant id, with an organization claim from the approval
    /// body, and allows every refresh with the stored claims.
    /// </summary>
    internal sealed class FakeOAuthGrantHandler : IOAuthGrantHandler
    {
        public List<OAuthApprovalContext> ApprovalCalls { get; } = new List<OAuthApprovalContext>();

        /// <summary>
        /// The signed-in person on each approval, read while the request was live.
        /// </summary>
        public List<string?> ApprovingUserIds { get; } = new List<string?>();

        public List<OAuthRefreshContext> RefreshCalls { get; } = new List<OAuthRefreshContext>();

        public Func<OAuthApprovalContext, OAuthApprovalDecision> Approve { get; set; } = DefaultApprove;

        public Func<OAuthRefreshContext, RefreshClaimsDecision> Refresh { get; set; } =
            context => RefreshClaimsDecision.Allow(context.StoredClaims, context.StoredRoles);

        public Task<OAuthApprovalDecision> ApproveAsync(OAuthApprovalContext context, CancellationToken cancellationToken)
        {
            // Locked: a test approving one request twice at once calls this from two requests together.
            lock (ApprovalCalls)
            {
                ApprovalCalls.Add(context);
                ApprovingUserIds.Add(context.HttpContext.GetUserId());
            }

            return Task.FromResult(Approve(context));
        }

        public Task<RefreshClaimsDecision> RefreshAsync(OAuthRefreshContext context, CancellationToken cancellationToken)
        {
            RefreshCalls.Add(context);
            return Task.FromResult(Refresh(context));
        }

        private static OAuthApprovalDecision DefaultApprove(OAuthApprovalContext context)
        {
            string organization = context.ApprovalBody?.TryGetProperty("organization", out System.Text.Json.JsonElement value) == true
                ? value.GetString() ?? "none"
                : "none";

            return OAuthApprovalDecision.Approve(new OAuthGrant
            {
                GrantId = "grant-" + Guid.NewGuid().ToString("N"),
                Subject = context.HttpContext.GetUserId()!,
                AuthType = "user",
                Claims = [new Claim("organization_id", organization)],
                Roles = ["member"],
            });
        }
    }
}
