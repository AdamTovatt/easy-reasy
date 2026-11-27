using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Extension methods for registering SAML sign-in services in the dependency injection container.
    /// </summary>
    public static class SamlAuthServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="ISamlAuthService"/> (scoped) and what it needs, and the sweep that deletes expired rows
        /// every <see cref="SamlAuthOptions.SweepInterval"/>. Register an <see cref="ISamlIdentityProviderResolver"/> and an
        /// <see cref="ISamlAuthStore"/> too; the resolver builds its identity providers with the registered
        /// <see cref="SamlIdentityProviderFactory"/>. To serve the sign-in endpoints and the assertion consumer service,
        /// register an <see cref="ISamlAuthHandler"/> and call
        /// <see cref="SamlAuthApplicationBuilderExtensions.AddSamlAuthEndpoints"/>.
        /// </summary>
        /// <remarks>
        /// Registers <see cref="TimeProvider.System"/> as the clock unless one is registered already; expiry, the assertion's
        /// times and the max authentication age are all checked against the registered clock.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="options">This application as a service provider.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">The options are invalid. Every problem is reported in one exception.</exception>
        public static IServiceCollection AddEasyReasySamlAuth(this IServiceCollection services, SamlAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);

            IReadOnlyList<string> problems = options.FindProblems();
            if (problems.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(SamlAuthOptions)}:\n{string.Join("\n", problems)}", nameof(options));
            }

            // Copied now, so the caller changing its arrays or lists later does not change the registration.
            SamlAuthOptions copy = new SamlAuthOptions
            {
                ServiceProviderEntityId = options.ServiceProviderEntityId,
                AssertionConsumerServiceUrl = options.AssertionConsumerServiceUrl,
                StateKey = options.StateKey.ToArray(),
                RequestLifetime = options.RequestLifetime,
                CompletionLifetime = options.CompletionLifetime,
                SweepInterval = options.SweepInterval,
                SigningCertificate = options.SigningCertificate,
                DecryptionCertificates = options.DecryptionCertificates.ToList(),
            };

            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton(copy);
            services.AddSingleton<SamlIdentityProviderFactory>();
            services.AddSingleton(serviceProvider => new SamlResponseValidator(
                copy.ServiceProviderEntityId,
                copy.AssertionConsumerServiceUrl,
                copy.DecryptionCertificates,
                serviceProvider.GetRequiredService<TimeProvider>()));
            services.AddScoped<SamlAuthService>();
            services.AddScoped<ISamlAuthService>(serviceProvider => serviceProvider.GetRequiredService<SamlAuthService>());
            services.AddHostedService<SamlAuthSweep>();

            return services;
        }
    }
}
