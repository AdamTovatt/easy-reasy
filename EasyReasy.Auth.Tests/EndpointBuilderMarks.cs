using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Proves an endpoint mapper hands back one builder per endpoint: <see cref="Mark"/> adds to each builder the name of
    /// the endpoint it is documented to configure, as <c>"POST /path"</c>, and
    /// <see cref="AssertEachReachesOnlyItsEndpoint"/> checks every one of those endpoints carries its own mark and no
    /// other. Linked into each sign-in package's test project.
    /// </summary>
    internal static class EndpointBuilderMarks
    {
        /// <summary>
        /// Marks each builder with the endpoint it is documented to configure. Call before the application starts.
        /// </summary>
        /// <param name="builderByEndpoint">Each builder by its endpoint's method and route pattern.</param>
        public static void Mark(IReadOnlyDictionary<string, RouteHandlerBuilder> builderByEndpoint)
        {
            foreach (KeyValuePair<string, RouteHandlerBuilder> entry in builderByEndpoint)
            {
                entry.Value.WithMetadata(new EndpointMark(entry.Key));
            }
        }

        /// <summary>
        /// Asserts each named endpoint is mapped and carries exactly its own mark.
        /// </summary>
        /// <param name="services">The started application's services.</param>
        /// <param name="endpointNames">The endpoints <see cref="Mark"/> was given.</param>
        public static void AssertEachReachesOnlyItsEndpoint(IServiceProvider services, IEnumerable<string> endpointNames)
        {
            List<RouteEndpoint> mapped = services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
            foreach (string name in endpointNames)
            {
                RouteEndpoint endpoint = mapped.Single(candidate => NameOf(candidate) == name);
                string[] marks = endpoint.Metadata.GetOrderedMetadata<EndpointMark>().Select(mark => mark.Endpoint).ToArray();
                CollectionAssert.AreEqual(new[] { name }, marks, $"{name} carries the marks [{string.Join(", ", marks)}].");
            }
        }

        private static string NameOf(RouteEndpoint endpoint)
        {
            IEnumerable<string> methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            return $"{string.Join(",", methods)} {endpoint.RoutePattern.RawText}";
        }

        private sealed record EndpointMark(string Endpoint);
    }
}
