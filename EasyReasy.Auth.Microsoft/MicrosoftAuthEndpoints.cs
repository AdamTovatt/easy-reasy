using Microsoft.AspNetCore.Builder;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The endpoints <see cref="MicrosoftAuthApplicationBuilderExtensions.AddMicrosoftAuthEndpoints"/> mapped, one builder
    /// each, so the application can add its own metadata to one endpoint (a rate limiting policy, a marker its middleware
    /// reads) without matching paths. A convention added here applies as if it were chained on the mapping itself.
    /// </summary>
    public sealed class MicrosoftAuthEndpoints
    {
        /// <summary>
        /// The sign-in begin endpoint at <c>POST /api/auth/microsoft/begin</c>.
        /// </summary>
        public required RouteHandlerBuilder Begin { get; init; }

        /// <summary>
        /// The sign-in endpoint at <c>POST /api/auth/microsoft</c>.
        /// </summary>
        public required RouteHandlerBuilder SignIn { get; init; }
    }
}
