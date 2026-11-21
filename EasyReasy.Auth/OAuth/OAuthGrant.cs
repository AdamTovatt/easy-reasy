using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What an approval grants: who the tokens are for and what they carry. <see cref="IOAuthGrantHandler.ApproveAsync"/>
    /// returns it after recording the grant in the application.
    /// </summary>
    public sealed class OAuthGrant
    {
        /// <summary>
        /// The application's id for the grant it recorded, such as a connection id. It is stored on the code and on every
        /// refresh token issued under the grant, passed to <see cref="IOAuthGrantHandler.RefreshAsync"/>, and is what
        /// <see cref="IOAuthGrantService.RevokeGrantAsync"/> takes to end the connection.
        /// </summary>
        public required string GrantId { get; init; }

        /// <summary>
        /// The subject the tokens are issued for (their <c>sub</c>). The application's choice: the approving person for an
        /// agent acting for that person, or a principal of its own for a system that outlives whoever approved it.
        /// </summary>
        public required string Subject { get; init; }

        /// <summary>
        /// The authentication type the tokens carry (<see cref="EasyReasyClaimTypes.AuthType"/>), such as <c>"user"</c>.
        /// </summary>
        public required string AuthType { get; init; }

        /// <summary>
        /// The additional claims the tokens carry. A <see cref="EasyReasyClaimTypes.ClientId"/> or
        /// <see cref="EasyReasyClaimTypes.RefreshFamilyId"/> claim here is dropped: the server sets both from its own
        /// records. Claims keep their type and value, not their value type, as with
        /// <see cref="IRefreshTokenService.StartSessionAsync"/>.
        /// </summary>
        public IReadOnlyList<Claim> Claims { get; init; } = [];

        /// <summary>
        /// The roles the tokens carry.
        /// </summary>
        public IReadOnlyList<string> Roles { get; init; } = [];
    }
}
