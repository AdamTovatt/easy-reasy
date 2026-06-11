using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class HttpContextExtensionsTests
    {
        [TestMethod]
        public void GetRoles_ReturnsAllRoles_WhenAuthenticated()
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "admin"),
                new Claim(ClaimTypes.Role, "user"),
            };
            ClaimsIdentity identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            DefaultHttpContext context = new DefaultHttpContext { User = principal };

            List<string> roles = context.GetRoles().ToList();

            CollectionAssert.AreEquivalent(new[] { "admin", "user" }, roles);
        }

        [TestMethod]
        public void GetRoles_ReturnsEmpty_WhenNotAuthenticated()
        {
            ClaimsIdentity identity = new ClaimsIdentity(); // Not authenticated
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            DefaultHttpContext context = new DefaultHttpContext { User = principal };

            List<string> roles = context.GetRoles().ToList();

            Assert.AreEqual(0, roles.Count);
        }

        [TestMethod]
        public void GetClaimValue_ByEnum_ReturnsExpectedValues()
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, "user-123"),
                new Claim("tenant_id", "tenant-42"),
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, "user@example.com"),
                new Claim("auth_type", "apikey"),
                new Claim("family_id", "family-7"),
            };
            ClaimsIdentity identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            DefaultHttpContext context = new DefaultHttpContext { User = principal };

            // Simulate ClaimsInjectionMiddleware
            context.Items["UserId"] = "user-123";
            context.Items["TenantId"] = "tenant-42";
            context.Items["Issuer"] = "issuer-xyz";

            Assert.AreEqual("user-123", context.GetClaimValue(EasyReasyClaim.UserId));
            Assert.AreEqual("tenant-42", context.GetClaimValue(EasyReasyClaim.TenantId));
            Assert.AreEqual("user@example.com", context.GetClaimValue(EasyReasyClaim.Email));
            Assert.AreEqual("apikey", context.GetClaimValue(EasyReasyClaim.AuthType));
            Assert.AreEqual("issuer-xyz", context.GetClaimValue(EasyReasyClaim.Issuer));
            Assert.AreEqual("family-7", context.GetClaimValue(EasyReasyClaim.RefreshFamilyId));
        }

        [TestMethod]
        public void GetClaimValue_ByEnum_ReturnsNull_WhenNotPresent()
        {
            ClaimsIdentity identity = new ClaimsIdentity();
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            DefaultHttpContext context = new DefaultHttpContext { User = principal };

            Assert.IsNull(context.GetClaimValue(EasyReasyClaim.UserId));
            Assert.IsNull(context.GetClaimValue(EasyReasyClaim.TenantId));
            Assert.IsNull(context.GetClaimValue(EasyReasyClaim.Email));
            Assert.IsNull(context.GetClaimValue(EasyReasyClaim.AuthType));
            Assert.IsNull(context.GetClaimValue(EasyReasyClaim.Issuer));
            Assert.IsNull(context.GetClaimValue(EasyReasyClaim.RefreshFamilyId));
        }

        [TestMethod]
        public void GetRefreshFamilyId_ReturnsClaimValue_WhenPresent()
        {
            List<Claim> claims = new List<Claim> { new Claim("family_id", "family-7") };
            ClaimsIdentity identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            DefaultHttpContext context = new DefaultHttpContext { User = principal };

            Assert.AreEqual("family-7", context.GetRefreshFamilyId());
        }

        [TestMethod]
        public void GetRefreshFamilyId_ReturnsNull_WhenAbsent()
        {
            ClaimsIdentity identity = new ClaimsIdentity(); // Not authenticated, no claims
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            DefaultHttpContext context = new DefaultHttpContext { User = principal };

            Assert.IsNull(context.GetRefreshFamilyId());
        }
    }
}
