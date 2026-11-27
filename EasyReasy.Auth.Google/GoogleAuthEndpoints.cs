using Microsoft.AspNetCore.Builder;

namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// The endpoints <see cref="GoogleAuthApplicationBuilderExtensions.AddGoogleAuthEndpoints"/> mapped, one builder each,
    /// so the application can add its own metadata to one endpoint (a rate limiting policy, a marker its middleware reads)
    /// without matching paths. A convention added here applies as if it were chained on the mapping itself.
    /// </summary>
    public sealed class GoogleAuthEndpoints
    {
        /// <summary>
        /// The sign-in endpoint at <c>POST /api/auth/google</c>.
        /// </summary>
        public required RouteHandlerBuilder SignIn { get; init; }
    }
}
