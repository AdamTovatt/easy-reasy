using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Extension methods for registering OpenID Connect sign-in services in the dependency injection container.
    /// </summary>
    public static class OpenIdConnectAuthServiceCollectionExtensions
    {
        /// <summary>
        /// The service key the application's <see cref="OpenIdConnectCodeFlow"/> is registered under, for the service and
        /// the sign-in endpoints to share it.
        /// </summary>
        internal const string ApplicationFlowKey = "EasyReasy.Auth.OpenIdConnect:application";

        /// <summary>
        /// Registers <see cref="IOpenIdConnectAuthService"/> and what it needs. Register an
        /// <see cref="IOpenIdConnectProviderResolver"/> too, which supplies the providers. To also serve the sign-in
        /// endpoints, register an <see cref="IOpenIdConnectAuthHandler"/> and call
        /// <see cref="OpenIdConnectAuthApplicationBuilderExtensions.AddOpenIdConnectAuthEndpoints"/>.
        /// </summary>
        /// <remarks>
        /// Registers <see cref="TimeProvider.System"/> as the clock unless one is registered already; state expiry, token
        /// lifetime and the max authentication age are all checked against the registered clock. Providers are called
        /// through the <see cref="OpenIdConnectCodeFlow.HttpClientName"/> client.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="options">The state key and lifetime.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">The options are invalid: a state key that is not 32 bytes, or a state
        /// lifetime that is not positive. Every problem is reported in one exception.</exception>
        public static IServiceCollection AddEasyReasyOpenIdConnectAuth(this IServiceCollection services, OpenIdConnectAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            IReadOnlyList<string> problems = options.FindProblems();
            if (problems.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(OpenIdConnectAuthOptions)}:\n{string.Join("\n", problems)}", nameof(options));
            }

            // Copied now, as the options document; the flow is built when first resolved.
            OpenIdConnectAuthOptions copy = new OpenIdConnectAuthOptions
            {
                StateKey = options.StateKey.ToArray(),
                StateLifetime = options.StateLifetime,
            };

            services.TryAddSingleton(TimeProvider.System);
            services.AddHttpClient(OpenIdConnectCodeFlow.HttpClientName);
            // Keyed, so it is the application's flow alone: a provider package builds its own under its own state key.
            services.AddKeyedSingleton(ApplicationFlowKey, (serviceProvider, _) => new OpenIdConnectCodeFlow(
                copy,
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                serviceProvider.GetRequiredService<TimeProvider>()));
            services.AddSingleton<IOpenIdConnectAuthService>(serviceProvider =>
                new OpenIdConnectAuthService(serviceProvider.GetRequiredKeyedService<OpenIdConnectCodeFlow>(ApplicationFlowKey)));

            return services;
        }
    }
}
