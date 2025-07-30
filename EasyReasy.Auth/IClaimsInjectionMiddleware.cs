using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Interface for middleware that injects claims (such as user and tenant id) into the HTTP context.
    /// </summary>
    public interface IClaimsInjectionMiddleware
    {
        /// <summary>
        /// Processes the HTTP request and injects claims into the context.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InvokeAsync(HttpContext context);
    }
}