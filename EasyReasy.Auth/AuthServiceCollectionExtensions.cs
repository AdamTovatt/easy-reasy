using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Extension methods for configuring authentication and authorization for EasyReasy.
    /// </summary>
    public static class AuthServiceCollectionExtensions
    {
        /// <summary>
        /// Adds JWT authentication and authorization policies for EasyReasy.
        /// Uses default options: no issuer/audience validation, 30-second clock skew,
        /// and automatic registration of <see cref="IJwtTokenService"/>.
        /// </summary>
        /// <param name="services">The service collection to add authentication to.</param>
        /// <param name="jwtSecret">The secret key used to sign JWT tokens.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEasyReasyAuth(
            this IServiceCollection services,
            string jwtSecret)
        {
            return AddEasyReasyAuth(services, jwtSecret, configure: null);
        }

        /// <summary>
        /// Adds JWT authentication and authorization policies for EasyReasy
        /// with the specified configuration options.
        /// </summary>
        /// <param name="services">The service collection to add authentication to.</param>
        /// <param name="jwtSecret">The secret key used to sign JWT tokens.</param>
        /// <param name="configure">An optional action to configure <see cref="EasyReasyAuthOptions"/>.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEasyReasyAuth(
            this IServiceCollection services,
            string jwtSecret,
            Action<EasyReasyAuthOptions>? configure)
        {
            EasyReasyAuthOptions options = new EasyReasyAuthOptions();
            configure?.Invoke(options);
            options.Validate();

            byte[] key = Encoding.UTF8.GetBytes(jwtSecret);

            services.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(bearerOptions =>
            {
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = options.Issuer != null,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = options.Audience != null,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = options.ClockSkew,
                };
            });

            services.AddAuthorization(authzOptions =>
            {
                authzOptions.AddPolicy("ApiKeyOnly", policy =>
                    policy.RequireClaim("auth_type", "apikey"));
                authzOptions.AddPolicy("UserOnly", policy =>
                    policy.RequireClaim("auth_type", "user"));
            });

            // Register JWT token service for dependency injection if requested
            if (options.RegisterJwtTokenService)
            {
                services.AddSingleton<IJwtTokenService>(
                    provider => new JwtTokenService(jwtSecret, options.Issuer, options.Audience));
            }

            return services;
        }

        /// <summary>
        /// Registers the <see cref="IPasswordResetTokenHandler"/> for dependency injection as a singleton.
        /// </summary>
        /// <param name="services">The service collection to add the handler to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddPasswordResetTokenHandler(this IServiceCollection services)
        {
            services.AddSingleton<IPasswordResetTokenHandler, SecurePasswordResetTokenHandler>();
            return services;
        }

        /// <summary>
        /// Registers the refresh token service and its backing store for dependency injection.
        /// The consumer-provided <typeparamref name="TStore"/> is registered as scoped.
        /// </summary>
        /// <typeparam name="TStore">
        /// The consumer's implementation of <see cref="IRefreshTokenStore"/> for persisting refresh tokens.
        /// </typeparam>
        /// <param name="services">The service collection to add the refresh token service to.</param>
        /// <param name="refreshTokenLifetime">
        /// The lifetime of refresh tokens. Defaults to 30 days if not specified.
        /// </param>
        /// <param name="accessTokenLifetime">
        /// The lifetime of access tokens created during refresh. Defaults to 1 hour if not specified.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddRefreshTokenService<TStore>(
            this IServiceCollection services,
            TimeSpan? refreshTokenLifetime = null,
            TimeSpan? accessTokenLifetime = null)
            where TStore : class, IRefreshTokenStore
        {
            services.AddScoped<IRefreshTokenStore, TStore>();
            services.AddScoped<IRefreshTokenService>(provider =>
            {
                IRefreshTokenStore store = provider.GetRequiredService<IRefreshTokenStore>();
                IAuthAuditLogger? auditLogger = provider.GetService<IAuthAuditLogger>();
                return new RefreshTokenService(store, refreshTokenLifetime, accessTokenLifetime, auditLogger);
            });

            return services;
        }
    }
}
