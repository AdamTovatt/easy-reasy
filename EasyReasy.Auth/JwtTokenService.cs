using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Service for creating JWT tokens for EasyReasy authentication.
    /// </summary>
    public class JwtTokenService : IJwtTokenService
    {
        private readonly byte[] _key;
        private readonly string? _issuer;

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
        /// </summary>
        /// <param name="secret">The secret key used to sign JWT tokens.</param>
        /// <param name="issuer">The issuer to use in the JWT tokens. If null, no issuer is set.</param>
        public JwtTokenService(string secret, string? issuer = null)
        {
            if (string.IsNullOrEmpty(secret))
                throw new ArgumentException("JWT secret cannot be null or empty", nameof(secret));

            byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
            if (secretBytes.Length < 32)
                throw new ArgumentException($"JWT secret must be at least 32 bytes (256 bits) for HS256. Current length: {secretBytes.Length} bytes", nameof(secret));

            _key = secretBytes;
            _issuer = issuer;
        }

        /// <summary>
        /// Creates a signed JWT token with the specified claims and roles.
        /// </summary>
        /// <param name="subject">The subject (user id) for the token.</param>
        /// <param name="authType">The authentication type (e.g., "apikey" or "user").</param>
        /// <param name="additionalClaims">Additional claims to include in the token.</param>
        /// <param name="roles">Roles to include in the token.</param>
        /// <param name="expiresAt">The expiration time for the token.</param>
        /// <returns>The signed JWT token as a string.</returns>
        public string CreateToken(
            string subject,
            string authType,
            IEnumerable<Claim> additionalClaims,
            IEnumerable<string> roles,
            DateTime expiresAt)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim("auth_type", authType),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            };
            if (additionalClaims != null)
                claims.AddRange(additionalClaims);
            if (roles != null)
            {
                foreach (string role in roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));
            }

            SigningCredentials credentials = new SigningCredentials(
                new SymmetricSecurityKey(_key),
                SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token = _issuer != null
                ? new JwtSecurityToken(
                    issuer: _issuer,
                    claims: claims,
                    expires: expiresAt,
                    signingCredentials: credentials)
                : new JwtSecurityToken(
                    claims: claims,
                    expires: expiresAt,
                    signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}