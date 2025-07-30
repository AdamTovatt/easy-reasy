using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Interface for a service that creates JWT tokens for EasyReasy authentication.
    /// </summary>
    public interface IJwtTokenService
    {
        /// <summary>
        /// Creates a signed JWT token with the specified claims and roles.
        /// </summary>
        /// <param name="subject">The subject (user id) for the token.</param>
        /// <param name="authType">The authentication type (e.g., "apikey" or "user").</param>
        /// <param name="additionalClaims">Additional claims to include in the token.</param>
        /// <param name="roles">Roles to include in the token.</param>
        /// <param name="expiresAt">The expiration time for the token.</param>
        /// <returns>The signed JWT token as a string.</returns>
        string CreateToken(
            string subject,
            string authType,
            IEnumerable<Claim> additionalClaims,
            IEnumerable<string> roles,
            DateTime expiresAt);
    }
}