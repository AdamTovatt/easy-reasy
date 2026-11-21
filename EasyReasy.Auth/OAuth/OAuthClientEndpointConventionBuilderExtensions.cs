using Microsoft.AspNetCore.Builder;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Extension methods for opening endpoints to OAuth connections.
    /// </summary>
    public static class OAuthClientEndpointConventionBuilderExtensions
    {
        /// <summary>
        /// Lets an OAuth connection's access token reach the endpoint, or every endpoint of a route group. See
        /// <see cref="AllowOAuthClientsAttribute"/>.
        /// </summary>
        /// <typeparam name="TBuilder">The endpoint convention builder.</typeparam>
        /// <param name="builder">The endpoint or route group.</param>
        /// <returns>The builder for chaining.</returns>
        public static TBuilder AllowOAuthClients<TBuilder>(this TBuilder builder)
            where TBuilder : IEndpointConventionBuilder
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.WithMetadata(new AllowOAuthClientsAttribute());
        }
    }
}
