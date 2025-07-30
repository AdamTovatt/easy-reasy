using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Middleware that injects user and tenant id claims into the HTTP context items.
    /// </summary>
    public class ClaimsInjectionMiddleware : IClaimsInjectionMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClaimsInjectionMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public ClaimsInjectionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Processes the HTTP request and injects user and tenant id claims into the context items.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                string? userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                string? tenantId = context.User.FindFirst("tenant_id")?.Value;
                context.Items["UserId"] = userId;
                context.Items["TenantId"] = tenantId;
            }
            await _next(context);
        }
    }
}