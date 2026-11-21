using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What <see cref="IOAuthGrantHandler.RefreshAsync"/> is deciding on: a client refreshing its tokens under a grant.
    /// </summary>
    public sealed class OAuthRefreshContext
    {
        /// <summary>
        /// The application's grant id (<see cref="OAuthGrant.GrantId"/>).
        /// </summary>
        public required string GrantId { get; init; }

        /// <summary>
        /// The client refreshing; it has authenticated, and the refresh token is bound to it.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// The subject the tokens are issued for.
        /// </summary>
        public required string Subject { get; init; }

        /// <summary>
        /// The authentication type the tokens carry.
        /// </summary>
        public required string AuthType { get; init; }

        /// <summary>
        /// The claims the previous access token was built from, without the <see cref="EasyReasyClaimTypes.ClientId"/> and
        /// <see cref="EasyReasyClaimTypes.RefreshFamilyId"/> claims the server adds itself.
        /// </summary>
        public required IReadOnlyList<Claim> StoredClaims { get; init; }

        /// <summary>
        /// The roles the previous access token carried.
        /// </summary>
        public required IReadOnlyList<string> StoredRoles { get; init; }

        /// <summary>
        /// The token request.
        /// </summary>
        public required HttpContext HttpContext { get; init; }
    }
}
