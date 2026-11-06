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
        /// The name of the <see cref="HttpClient"/> the package calls Microsoft with (the token endpoint, the OpenID
        /// configuration and the signing keys). Configure it with <c>services.AddHttpClient(name)</c> to add a proxy
        /// or a timeout.
        /// </summary>
        public const string HttpClientName = "EasyReasy.Auth.Microsoft";

        /// <summary>
        /// Registers <see cref="IMicrosoftAuthService"/> and what it needs. To also serve the sign-in endpoints,
        /// register an <c>IMicrosoftAuthHandler</c> and call <c>AddMicrosoftAuthEndpoints</c>.
        /// </summary>
        /// <remarks>
        /// Registers <see cref="TimeProvider.System"/> as the clock unless one is registered already; state expiry,
        /// token lifetime and the max authentication age are all checked against the registered clock.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="options">The Microsoft options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The options are invalid: a blank client id or secret, a state key that is not 32 bytes, no redirect URI,
        /// a redirect URI or authority that is not an absolute https URL (or http on a loopback host), the personal
        /// accounts tenant in the tenant allowlist, or a state lifetime that is not positive. Every problem is reported
        /// in one exception.
        /// </exception>
        public static IServiceCollection AddEasyReasyMicrosoftAuth(this IServiceCollection services, MicrosoftAuthOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);
            ValidateOptions(options);

            services.AddSingleton(options);
            services.TryAddSingleton(TimeProvider.System);
            services.AddHttpClient(HttpClientName);
            services.AddSingleton(new MicrosoftAuthority(options.AuthorityBaseUrl));
            // Built here so the key is copied now, as the options document.
            services.AddSingleton(new MicrosoftAuthStateProtector(options.StateKey));
            services.AddSingleton<MicrosoftTokenEndpointClient>();
            services.AddSingleton<MicrosoftIdTokenValidator>();
            services.AddSingleton<IMicrosoftAuthService, MicrosoftAuthService>();

            return services;
        }

        private static void ValidateOptions(MicrosoftAuthOptions options)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.ClientId)} must not be blank.");
            }

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.ClientSecret)} must not be blank.");
            }

            if (options.StateKey == null || options.StateKey.Length != 32)
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.StateKey)} must be exactly 32 bytes for AES-256; got {options.StateKey?.Length.ToString() ?? "null"}.");
            }

            if (options.RedirectUris == null || options.RedirectUris.Count == 0)
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.RedirectUris)} must hold at least one redirect URI.");
            }
            else
            {
                foreach (string? redirectUri in options.RedirectUris)
                {
                    if (!MicrosoftAuthority.TryParseAllowedAddress(redirectUri, out Uri? uri) || !string.IsNullOrEmpty(uri.Fragment))
                    {
                        errors.Add($"The redirect URI '{redirectUri}' must be an absolute https URL (or http on a loopback host) without a fragment.");
                    }
                }
            }

            if (options.AllowedTenantIds?.Contains(MicrosoftAuthority.PersonalAccountsTenantId) == true)
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.AllowedTenantIds)} holds the personal Microsoft accounts tenant, whose accounts are always refused.");
            }

            if (!MicrosoftAuthority.TryParseAllowedAddress(options.AuthorityBaseUrl, out Uri? authority)
                || !string.IsNullOrEmpty(authority.Query)
                || !string.IsNullOrEmpty(authority.Fragment))
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.AuthorityBaseUrl)} '{options.AuthorityBaseUrl}' must be an absolute https URL (or http on a loopback host) without a query or fragment.");
            }

            if (options.StateLifetime <= TimeSpan.Zero)
            {
                errors.Add($"{nameof(MicrosoftAuthOptions.StateLifetime)} must be positive; got {options.StateLifetime}.");
            }

            if (errors.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(MicrosoftAuthOptions)}:\n{string.Join("\n", errors)}", nameof(options));
            }
        }
    }
}
