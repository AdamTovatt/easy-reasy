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

        [TestMethod]
        public void CreateToken_ShouldContainJtiClaim()
        {
            JwtSecurityToken jwt = CreateDefaultToken();
            string? jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

            Assert.IsNotNull(jti);
            Assert.IsTrue(Guid.TryParse(jti, out _));
        }

        [TestMethod]
        public void CreateToken_ShouldContainUniqueJtiPerToken()
        {
            JwtSecurityToken jwt1 = CreateDefaultToken();
            JwtSecurityToken jwt2 = CreateDefaultToken();

            string jti1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
            string jti2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            Assert.AreNotEqual(jti1, jti2);
        }

        [TestMethod]
        public void CreateToken_ShouldContainNbfClaim()
        {
            DateTime beforeCreation = DateTime.UtcNow;

            JwtSecurityToken jwt = CreateDefaultToken();
            string? nbf = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Nbf)?.Value;

            Assert.IsNotNull(nbf);
            long nbfUnix = long.Parse(nbf);
            DateTime nbfTime = DateTimeOffset.FromUnixTimeSeconds(nbfUnix).UtcDateTime;
            Assert.IsTrue(nbfTime >= beforeCreation.AddSeconds(-1));
            Assert.IsTrue(nbfTime <= DateTime.UtcNow.AddSeconds(1));
        }

        private static JwtSecurityToken CreateDefaultToken()
        {
            IJwtTokenService service = new JwtTokenService(Secret, Issuer);
            string token = service.CreateToken("user-1", "user", Array.Empty<Claim>(), Array.Empty<string>(), DateTime.UtcNow.AddHours(1));
            return new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
    }
}
