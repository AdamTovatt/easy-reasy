using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The claims the authorization server sets on a client's tokens itself, <see cref="EasyReasyClaimTypes.ClientId"/>
    /// and <see cref="EasyReasyClaimTypes.RefreshFamilyId"/>: never taken from the grant handler, never shown to it.
    /// </summary>
    internal static class OAuthServerClaims
    {
        /// <summary>
        /// Whether a principal carries a <see cref="EasyReasyClaimTypes.ClientId"/> claim, as a connection's token does.
        /// </summary>
        /// <param name="user">The principal.</param>
        /// <returns>Whether it is a connection.</returns>
        public static bool IsConnection(ClaimsPrincipal user)
        {
            return user.HasClaim(claim => claim.Type == EasyReasyClaimTypes.ClientId);
        }

        /// <summary>
        /// The claims with the server's own taken out.
        /// </summary>
        /// <param name="claims">The claims.</param>
        /// <returns>The claims without the server's own.</returns>
        public static List<Claim> Without(IEnumerable<Claim> claims)
        {
            return claims.Where(claim => claim.Type != EasyReasyClaimTypes.ClientId && claim.Type != EasyReasyClaimTypes.RefreshFamilyId).ToList();
        }

        /// <summary>
        /// The claims without the server's own, plus the client id claim. The family id claim is added by
        /// <see cref="RefreshTokenService"/> when it issues the token.
        /// </summary>
        /// <param name="claims">The claims.</param>
        /// <param name="clientId">The client id.</param>
        /// <returns>The claims a client's token carries.</returns>
        public static List<Claim> WithClientId(IEnumerable<Claim> claims, string clientId)
        {
            List<Claim> result = Without(claims);
            result.Add(new Claim(EasyReasyClaimTypes.ClientId, clientId));
            return result;
        }
    }
}
