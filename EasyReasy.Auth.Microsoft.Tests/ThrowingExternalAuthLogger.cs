using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// An <see cref="IAuthAuditLogger"/> whose external sign-in hook always throws.
    /// </summary>
    internal sealed class ThrowingExternalAuthLogger : IAuthAuditLogger
    {
        public Task OnExternalAuthAsync(HttpContext httpContext, ExternalAuthResult result)
        {
            throw new InvalidOperationException("audit logger failure");
        }
    }
}
