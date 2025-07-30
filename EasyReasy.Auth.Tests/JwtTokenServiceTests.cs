using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class JwtTokenServiceTests
    {
        private const string Secret = "super_secret_key_12345_12345_12345";
        private const string Issuer = "test-issuer";

        [TestMethod]
        public void CreateToken_ShouldContainExpectedClaimsAndIssuer()
        {
            IJwtTokenService service = new JwtTokenService(Secret, Issuer);
            DateTime expires = DateTime.UtcNow.AddHours(1);
            Claim[] additionalClaims = new[] { new Claim("tenant_id", "tenant-42") };
            string[] roles = new[] { "admin", "user" };

            string token = service.CreateToken(
                subject: "user-123",
                authType: "apikey",
                additionalClaims: additionalClaims,
                roles: roles,
                expiresAt: expires);

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            TokenValidationParameters validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                ClockSkew = TimeSpan.Zero,
            };

            handler.ValidateToken(token, validationParams, out SecurityToken validatedToken);
            JwtSecurityToken jwt = (JwtSecurityToken)validatedToken;

            Assert.AreEqual("user-123", jwt.Subject);
            Assert.AreEqual(Issuer, jwt.Issuer);
            Assert.AreEqual("apikey", jwt.Claims.First(c => c.Type == "auth_type").Value);
            Assert.AreEqual("tenant-42", jwt.Claims.First(c => c.Type == "tenant_id").Value);
            CollectionAssert.IsSubsetOf(new[] { "admin", "user" }, jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList());
        }
    }
}