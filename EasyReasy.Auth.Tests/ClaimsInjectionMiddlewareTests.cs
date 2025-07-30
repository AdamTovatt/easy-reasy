using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class ClaimsInjectionMiddlewareTests
    {
        [TestMethod]
        public async Task InvokeAsync_ShouldInjectUserAndTenantId_WhenAuthenticated()
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, "user-123"),
                new Claim("tenant_id", "tenant-42"),
            };

            ClaimsIdentity identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            DefaultHttpContext context = new DefaultHttpContext { User = principal };
            bool nextCalled = false;
            RequestDelegate next = ctx => { nextCalled = true; return Task.CompletedTask; };
            ClaimsInjectionMiddleware middleware = new ClaimsInjectionMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.IsTrue(nextCalled);
            Assert.AreEqual("user-123", context.Items["UserId"]);
            Assert.AreEqual("tenant-42", context.Items["TenantId"]);
        }

        [TestMethod]
        public async Task InvokeAsync_ShouldNotInject_WhenNotAuthenticated()
        {
            ClaimsIdentity identity = new ClaimsIdentity(); // Not authenticated
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            DefaultHttpContext context = new DefaultHttpContext { User = principal };
            bool nextCalled = false;
            RequestDelegate next = ctx => { nextCalled = true; return Task.CompletedTask; };
            ClaimsInjectionMiddleware middleware = new ClaimsInjectionMiddleware(next);

            await middleware.InvokeAsync(context);

            Assert.IsTrue(nextCalled);
            Assert.IsFalse(context.Items.ContainsKey("UserId"));
            Assert.IsFalse(context.Items.ContainsKey("TenantId"));
        }
    }
}