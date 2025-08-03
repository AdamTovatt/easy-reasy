using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class HttpContextValidationTests
    {
        private const string Secret = "super_secret_key_12345_12345_12345";
        private const string Issuer = "test-issuer";

        [TestMethod]
        public void ValidateApiKeyRequestAsync_WithHttpContext_ShouldExtractHeaders()
        {
            // Arrange
            TestHttpContextValidationService validationService = new TestHttpContextValidationService();
            JwtTokenService jwtTokenService = new JwtTokenService(Secret, Issuer);
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("test-api-key");

            DefaultHttpContext httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Tenant-ID"] = "tenant-123";
            httpContext.Request.QueryString = new QueryString("?org=test-org");

            // Act
            AuthResponse? response = validationService.ValidateApiKeyRequestAsync(request, jwtTokenService, httpContext).Result;

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Token);

            // Verify the token contains the extracted header information
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            System.IdentityModel.Tokens.Jwt.JwtSecurityToken token = handler.ReadJwtToken(response.Token);

            Claim? tenantClaim = token.Claims.FirstOrDefault(c => c.Type == "tenant_id");
            Assert.IsNotNull(tenantClaim);
            Assert.AreEqual("tenant-123", tenantClaim.Value);
        }

        [TestMethod]
        public void ValidateLoginRequestAsync_WithHttpContext_ShouldExtractHeaders()
        {
            // Arrange
            TestHttpContextValidationService validationService = new TestHttpContextValidationService();
            JwtTokenService jwtTokenService = new JwtTokenService(Secret, Issuer);
            LoginAuthRequest request = new LoginAuthRequest("test-user", "test-password");

            DefaultHttpContext httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Tenant-ID"] = "tenant-456";
            httpContext.Request.QueryString = new QueryString("?org=test-org");

            // Act
            AuthResponse? response = validationService.ValidateLoginRequestAsync(request, jwtTokenService, httpContext).Result;

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Token);

            // Verify the token contains the extracted header information
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            System.IdentityModel.Tokens.Jwt.JwtSecurityToken token = handler.ReadJwtToken(response.Token);

            Claim? tenantClaim = token.Claims.FirstOrDefault(c => c.Type == "tenant_id");
            Assert.IsNotNull(tenantClaim);
            Assert.AreEqual("tenant-456", tenantClaim.Value);
        }

        [TestMethod]
        public void ValidateApiKeyRequestAsync_WithoutHttpContext_ShouldWorkBackwardCompatible()
        {
            // Arrange
            TestHttpContextValidationService validationService = new TestHttpContextValidationService();
            JwtTokenService jwtTokenService = new JwtTokenService(Secret, Issuer);
            ApiKeyAuthRequest request = new ApiKeyAuthRequest("test-api-key");

            // Act
            AuthResponse? response = validationService.ValidateApiKeyRequestAsync(request, jwtTokenService, null).Result;

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Token);

            // Verify the token contains default tenant information
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            System.IdentityModel.Tokens.Jwt.JwtSecurityToken token = handler.ReadJwtToken(response.Token);

            Claim? tenantClaim = token.Claims.FirstOrDefault(c => c.Type == "tenant_id");
            Assert.IsNotNull(tenantClaim);
            Assert.AreEqual("default-tenant", tenantClaim.Value);
        }

        [TestMethod]
        public void ValidateLoginRequestAsync_WithoutHttpContext_ShouldWorkBackwardCompatible()
        {
            // Arrange
            TestHttpContextValidationService validationService = new TestHttpContextValidationService();
            JwtTokenService jwtTokenService = new JwtTokenService(Secret, Issuer);
            LoginAuthRequest request = new LoginAuthRequest("test-user", "test-password");

            // Act
            AuthResponse? response = validationService.ValidateLoginRequestAsync(request, jwtTokenService, null).Result;

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Token);

            // Verify the token contains default tenant information
            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            System.IdentityModel.Tokens.Jwt.JwtSecurityToken token = handler.ReadJwtToken(response.Token);

            Claim? tenantClaim = token.Claims.FirstOrDefault(c => c.Type == "tenant_id");
            Assert.IsNotNull(tenantClaim);
            Assert.AreEqual("default-tenant", tenantClaim.Value);
        }

        private class TestHttpContextValidationService : IAuthRequestValidationService
        {
            public Task<AuthResponse?> ValidateApiKeyRequestAsync(ApiKeyAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
            {
                // Extract tenant ID from header if available
                string tenantId = "default-tenant";
                if (httpContext?.Request.Headers.TryGetValue("X-Tenant-ID", out StringValues headerTenantId) == true)
                {
                    tenantId = headerTenantId.ToString();
                }

                // Create JWT token with tenant information
                DateTime expiresAt = DateTime.UtcNow.AddHours(1);
                string token = jwtTokenService.CreateToken(
                    subject: "test-user",
                    authType: "apikey",
                    additionalClaims: new[] { new Claim("tenant_id", tenantId) },
                    roles: new[] { "user" },
                    expiresAt: expiresAt);

                return Task.FromResult<AuthResponse?>(new AuthResponse(token, expiresAt.ToString("o")));
            }

            public Task<AuthResponse?> ValidateLoginRequestAsync(LoginAuthRequest request, IJwtTokenService jwtTokenService, HttpContext? httpContext = null)
            {
                // Extract tenant ID from header if available
                string tenantId = "default-tenant";
                if (httpContext?.Request.Headers.TryGetValue("X-Tenant-ID", out StringValues headerTenantId) == true)
                {
                    tenantId = headerTenantId.ToString();
                }

                // Create JWT token with tenant information
                DateTime expiresAt = DateTime.UtcNow.AddHours(1);
                string token = jwtTokenService.CreateToken(
                    subject: "test-user",
                    authType: "user",
                    additionalClaims: new[] { new Claim("tenant_id", tenantId) },
                    roles: new[] { "user" },
                    expiresAt: expiresAt);

                return Task.FromResult<AuthResponse?>(new AuthResponse(token, expiresAt.ToString("o")));
            }
        }
    }
}