using EasyReasy.Auth.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Extension methods for registering Microsoft sign-in services in the dependency injection container.
    /// </summary>
    public static class MicrosoftAuthServiceCollectionExtensions
    {
        /// <summary>
        /// The service key the package's own <see cref="OpenIdConnectCodeFlow"/> is registered under, beside an
        /// application's own OpenID Connect flow, each under its own state key.
        /// </summary>
        private const string MicrosoftFlowKey = "EasyReasy.Auth.Microsoft:flow";

        /// <summary>
        /// Registers <see cref="IMicrosoftAuthService"/> and what it needs. To also serve the sign-in endpoints,
        /// register an <see cref="IMicrosoftAuthHandler"/> and call
        /// <see cref="MicrosoftAuthApplicationBuilderExtensions.AddMicrosoftAuthEndpoints"/>.
        /// </summary>
        /// <remarks>
        /// Registers <see cref="TimeProvider.System"/> as the clock unless one is registered already; state expiry,
        /// token lifetime and the max authentication age are all checked against the registered clock. Microsoft is
        /// called through the <see cref="OpenIdConnectCodeFlow.HttpClientName"/> client.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="options">The Microsoft options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The options are invalid: a blank client id or secret, a state key that is not 32 bytes, no redirect URI,
        /// a redirect URI that is not an absolute https URL (or http on <c>localhost</c>, a name under it, or a loopback
        /// address), an authority that is not an absolute https URL (or http on a loopback host), the personal accounts
        /// tenant in the tenant allowlist, or a state lifetime that is not positive. Every problem is reported in one
        /// exception.
        /// </exception>
        public static IServiceCollection AddEasyReasyMicrosoftAuth(this IServiceCollection services, MicrosoftAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            // Copied now, as the options document: the key here, the redirect URIs by the resolver's provider, and the
            // tenants by the set, so a caller changing them afterwards changes nothing.
            OpenIdConnectAuthOptions flowOptions = new OpenIdConnectAuthOptions
            {
                StateKey = options.StateKey?.ToArray() ?? [],
                StateLifetime = options.StateLifetime,
            };
            MicrosoftProviderResolver resolver = CreateValidatedResolver(options, flowOptions);
            IReadOnlySet<Guid> allowedTenantIds = (options.AllowedTenantIds ?? []).ToHashSet();

            services.TryAddSingleton(TimeProvider.System);
            services.AddHttpClient(OpenIdConnectCodeFlow.HttpClientName);
            services.AddKeyedSingleton(MicrosoftFlowKey, (serviceProvider, _) => new OpenIdConnectCodeFlow(
                flowOptions,
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                serviceProvider.GetRequiredService<TimeProvider>()));
            services.AddSingleton<IMicrosoftAuthService>(serviceProvider => new MicrosoftAuthService(
                serviceProvider.GetRequiredKeyedService<OpenIdConnectCodeFlow>(MicrosoftFlowKey),
                resolver,
                allowedTenantIds));

            return services;
        }

        /// <summary>
        /// Checks the options, every problem at once, and builds the resolver from them. The app registration's own
        /// values are checked by the shared flow's provider rules.
        /// </summary>
        private static MicrosoftProviderResolver CreateValidatedResolver(MicrosoftAuthOptions options, OpenIdConnectAuthOptions flowOptions)
        {
            List<string> errors = flowOptions.FindProblems().ToList();
            errors.AddRange(MicrosoftProviderResolver.FindProblems(options.ClientId, options.ClientSecret, options.RedirectUris));

            if (options.AllowedTenantIds?.Contains(MicrosoftAuthority.PersonalAccountsTenantId) == true)
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.AllowedTenantIds)} holds the personal Microsoft accounts tenant, whose accounts are always refused.");
            }

            MicrosoftAuthority? authority = null;
            try
            {
                authority = new MicrosoftAuthority(options.AuthorityBaseUrl);
            }
            catch (ArgumentException)
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.AuthorityBaseUrl)} '{options.AuthorityBaseUrl}' must be an absolute https URL (or http on a loopback host) without a query or fragment.");
            }

            if (errors.Count > 0 || authority == null)
            {
                throw new ArgumentException($"Invalid {nameof(MicrosoftAuthOptions)}:\n{string.Join("\n", errors)}", nameof(options));
            }

            return new MicrosoftProviderResolver(authority, options.ClientId, options.ClientSecret, options.RedirectUris);
        }
    }
}
