using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
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
        /// Whether a principal was authenticated by a connection's token: one carrying a
        /// <see cref="EasyReasyClaimTypes.ClientId"/> claim. With <see cref="IsConnection(SecurityToken)"/>, the one test of
        /// it: for the endpoints a connection may reach and the consent endpoints it may never reach.
        /// </summary>
        /// <param name="user">The principal.</param>
        /// <returns>Whether it is a connection.</returns>
        public static bool IsConnection(ClaimsPrincipal user)
        {
            return user.HasClaim(claim => claim.Type == EasyReasyClaimTypes.ClientId);
        }

        /// <summary>
        /// Whether a token being validated is a connection's, by the same claim. The bearer hands over a
        /// <see cref="JsonWebToken"/>, or a <see cref="JwtSecurityToken"/> with the older validators; any other kind is
        /// taken as a connection's, so a token the check cannot read is held to the stricter rule.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>Whether it is a connection's.</returns>
        public static bool IsConnection(SecurityToken token)
        {
            return token switch
            {
                JsonWebToken jsonWebToken => jsonWebToken.TryGetClaim(EasyReasyClaimTypes.ClientId, out _),
                JwtSecurityToken jwtSecurityToken => jwtSecurityToken.Claims.Any(claim => claim.Type == EasyReasyClaimTypes.ClientId),
                _ => true,
            };
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
