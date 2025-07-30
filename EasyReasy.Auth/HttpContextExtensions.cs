using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Extension methods for accessing user and tenant id from the HTTP context.
    /// </summary>
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Gets the tenant id from the HTTP context, if available.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The tenant id, or null if not available.</returns>
        public static string? GetTenantId(this HttpContext context)
        {
            if (context == null)
                return null;
            if (context.Items.TryGetValue("TenantId", out object? value))
                return value as string;
            return null;
        }

        /// <summary>
        /// Gets the user id from the HTTP context, if available.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The user id, or null if not available.</returns>
        public static string? GetUserId(this HttpContext context)
        {
            if (context == null)
                return null;
            if (context.Items.TryGetValue("UserId", out object? value))
                return value as string;
            return null;
        }

        /// <summary>
        /// Gets all roles for the current user from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>An enumerable of role names, or an empty enumerable if none are present.</returns>
        public static IEnumerable<string> GetRoles(this HttpContext context)
        {
            if (context?.User?.Identity == null || !context.User.Identity.IsAuthenticated)
                return Enumerable.Empty<string>();
            return context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value);
        }

        /// <summary>
        /// Gets the value of a claim by its type/key from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="claimType">The claim type/key to look for.</param>
        /// <returns>The claim value, or null if not found.</returns>
        public static string? GetClaimValue(this HttpContext context, string claimType)
        {
            if (context?.User?.Identity == null || !context.User.Identity.IsAuthenticated)
                return null;
            return context.User.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        }

        /// <summary>
        /// Gets the value of a claim by its enum type from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="claim">The claim enum to look for.</param>
        /// <returns>The claim value, or null if not found.</returns>
        public static string? GetClaimValue(this HttpContext context, EasyReasyClaim claim)
        {
            if (context == null)
                return null;
            switch (claim)
            {
                case EasyReasyClaim.UserId:
                    // Prefer Items, fallback to Claims
                    return context.Items.TryGetValue("UserId", out object? userId) ? userId as string : context.GetClaimValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
                case EasyReasyClaim.TenantId:
                    return context.Items.TryGetValue("TenantId", out object? tenantId) ? tenantId as string : context.GetClaimValue("tenant_id");
                case EasyReasyClaim.Email:
                    return context.GetClaimValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
                case EasyReasyClaim.AuthType:
                    return context.GetClaimValue("auth_type");
                case EasyReasyClaim.Issuer:
                    // JWT Issuer is not a claim, but a property on the token. Try to get it from ClaimsPrincipal if available.
                    ClaimsIdentity? jwt = context.User.Identity as ClaimsIdentity;
                    if (jwt != null && jwt.BootstrapContext is System.IdentityModel.Tokens.Jwt.JwtSecurityToken token)
                        return token.Issuer;
                    // Fallback: try to get from Items if set by custom middleware
                    return context.Items.TryGetValue("Issuer", out object? issuer) ? issuer as string : null;
                default:
                    return null;
            }
        }
    }
}