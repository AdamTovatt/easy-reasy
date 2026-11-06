using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// An <see cref="IAuthAuditLogger"/> whose external sign-in hook always throws, to check that a sign-in endpoint
    /// lets the failure propagate. Linked into each provider package's test project.
    /// </summary>
    internal sealed class ThrowingExternalAuthLogger : IAuthAuditLogger
    {
        public Task OnExternalAuthAsync(HttpContext httpContext, ExternalAuthResult result)
        {
            throw new InvalidOperationException("audit logger failure");
        }
    }
}
