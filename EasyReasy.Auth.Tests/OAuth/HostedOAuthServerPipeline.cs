namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// How <see cref="HostedOAuthServer"/> wires authentication into its pipeline.
    /// </summary>
    public enum HostedOAuthServerPipeline
    {
        /// <summary>
        /// <c>UseEasyReasyAuth</c>, after the routing a minimal-hosting app adds at the start.
        /// </summary>
        UseEasyReasyAuth,

        /// <summary>
        /// <c>UseAuthentication</c> and <c>UseAuthorization</c> by hand, without <c>UseEasyReasyAuth</c>.
        /// </summary>
        AuthenticationAndAuthorizationByHand,

        /// <summary>
        /// <c>UseAuthentication</c> before an explicit <c>UseRouting</c>, so no endpoint is chosen while the default
        /// scheme authenticates.
        /// </summary>
        RoutingAfterAuthentication,
    }
}
