using Microsoft.AspNetCore.Builder;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The endpoints <see cref="SamlAuthApplicationBuilderExtensions.AddSamlAuthEndpoints"/> mapped, one builder each, so
    /// the application can add its own metadata to one endpoint (a rate limiting policy, a marker its middleware reads)
    /// without matching paths. A convention added here applies as if it were chained on the mapping itself.
    /// </summary>
    /// <remarks>
    /// The assertion consumer service takes a form post from anyone and parses its XML before it can tell whether the
    /// response answers a request this server began, so it is the endpoint a rate limit matters most on.
    /// </remarks>
    public sealed class SamlAuthEndpoints
    {
        /// <summary>
        /// The assertion consumer service at <see cref="SamlAuthPaths.AssertionConsumerService"/>.
        /// </summary>
        public required RouteHandlerBuilder AssertionConsumerService { get; init; }

        /// <summary>
        /// The sign-in begin endpoint at <see cref="SamlAuthPaths.Begin"/>.
        /// </summary>
        public required RouteHandlerBuilder Begin { get; init; }

        /// <summary>
        /// The sign-in endpoint at <see cref="SamlAuthPaths.SignIn"/>.
        /// </summary>
        public required RouteHandlerBuilder SignIn { get; init; }
    }
}
