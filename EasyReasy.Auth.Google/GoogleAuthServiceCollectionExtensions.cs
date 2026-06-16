using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Extension methods for registering Google authentication services in the dependency injection container.
    /// </summary>
    public static class GoogleAuthServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the Google authentication services required for validating Google ID tokens
        /// and handling Google sign-in requests.
        /// The consumer must also register their own <see cref="IGoogleAuthHandler"/> implementation.
        /// </summary>
        /// <param name="services">The service collection to add Google auth services to.</param>
        /// <param name="googleClientId">The Google OAuth 2.0 client ID used to validate ID tokens.</param>
        /// <param name="allowedHostedDomains">
        /// An optional collection of allowed Google Workspace hosted domains.
        /// When set, only users from these domains will be accepted. Matching is case-insensitive.
        /// </param>
        /// <param name="requireVerifiedEmail">
        /// Whether to reject Google accounts whose email address is not verified. Defaults to <c>true</c>,
        /// matching Google's guidance that an unverified email must not be trusted as an identifier.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEasyReasyGoogleAuth(
            this IServiceCollection services,
            string googleClientId,
            ICollection<string>? allowedHostedDomains = null,
            bool requireVerifiedEmail = true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(googleClientId);

            GoogleAuthOptions options = new GoogleAuthOptions
            {
                ClientId = googleClientId,
                AllowedHostedDomains = allowedHostedDomains,
                RequireVerifiedEmail = requireVerifiedEmail,
            };

            services.AddSingleton(options);
            services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
            services.AddScoped<GoogleAuthService>();

            return services;
        }
    }
}
