using Microsoft.AspNetCore.Builder;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The endpoints <see cref="OAuthServerApplicationBuilderExtensions.AddOAuthServerEndpoints"/> mapped, one builder
    /// each, so the application can add its own metadata to one endpoint (a rate limiting policy, a marker its middleware
    /// reads) without matching paths. A convention added here applies as if it were chained on the mapping itself.
    /// </summary>
    public sealed class OAuthServerEndpoints
    {
        /// <summary>
        /// The authorization server metadata (RFC 8414) at <see cref="OAuthServerPaths.AuthorizationServerMetadata"/>.
        /// </summary>
        public required RouteHandlerBuilder AuthorizationServerMetadata { get; init; }

        /// <summary>
        /// The protected resource metadata (RFC 9728), at <see cref="OAuthServerPaths.ProtectedResourceMetadataPrefix"/>
        /// plus the resource's path.
        /// </summary>
        public required RouteHandlerBuilder ProtectedResourceMetadata { get; init; }

        /// <summary>
        /// Dynamic client registration (RFC 7591) at <see cref="OAuthServerPaths.Register"/>, or null when
        /// <see cref="OAuthServerOptions.AllowDynamicClientRegistration"/> is off and the endpoint is not mapped.
        /// </summary>
        public required RouteHandlerBuilder? Registration { get; init; }

        /// <summary>
        /// The authorization endpoint at <see cref="OAuthServerPaths.Authorize"/>.
        /// </summary>
        public required RouteHandlerBuilder Authorization { get; init; }

        /// <summary>
        /// The token endpoint at <see cref="OAuthServerPaths.Token"/>.
        /// </summary>
        public required RouteHandlerBuilder Token { get; init; }

        /// <summary>
        /// The revocation endpoint (RFC 7009) at <see cref="OAuthServerPaths.Revoke"/>.
        /// </summary>
        public required RouteHandlerBuilder Revocation { get; init; }

        /// <summary>
        /// The consent page's read of a pending request, <c>GET</c> under
        /// <see cref="OAuthServerPaths.AuthorizationRequestsPrefix"/>.
        /// </summary>
        public required RouteHandlerBuilder ConsentRequest { get; init; }

        /// <summary>
        /// The consent page's approval of a pending request, <c>POST</c> under
        /// <see cref="OAuthServerPaths.AuthorizationRequestsPrefix"/>, ending <c>/approve</c>.
        /// </summary>
        public required RouteHandlerBuilder ConsentApproval { get; init; }

        /// <summary>
        /// The consent page's denial of a pending request, <c>POST</c> under
        /// <see cref="OAuthServerPaths.AuthorizationRequestsPrefix"/>, ending <c>/deny</c>.
        /// </summary>
        public required RouteHandlerBuilder ConsentDenial { get; init; }
    }
}
