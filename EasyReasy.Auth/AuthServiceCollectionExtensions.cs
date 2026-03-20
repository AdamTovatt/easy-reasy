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
        /// The default clock skew tolerance for JWT token validation.
        /// This is intentionally lower than the <c>Microsoft.IdentityModel</c> default of 5 minutes
        /// to reduce the window in which an expired token is still accepted.
        /// </summary>
        private static readonly TimeSpan DefaultClockSkew = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Adds JWT authentication and authorization policies for EasyReasy.
        /// Also registers the IJwtTokenService for dependency injection.
        /// </summary>
        /// <param name="services">The service collection to add authentication to.</param>
        /// <param name="jwtSecret">The secret key used to sign JWT tokens.</param>
        /// <param name="issuer">The expected issuer for JWT tokens. If null, issuer validation is disabled.</param>
        /// <param name="clockSkew">
        /// Optional clock skew tolerance for token lifetime validation. Defaults to 30 seconds.
        /// The <c>Microsoft.IdentityModel</c> default is 5 minutes, which is often too generous.
        /// Increase this if you see tokens rejected due to clock drift between servers.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEasyReasyAuth(
            this IServiceCollection services,
            string jwtSecret,
            string? issuer = null,
            TimeSpan? clockSkew = null)
        {
            return AddEasyReasyAuth(services, jwtSecret, issuer, registerJwtTokenService: true, clockSkew: clockSkew);
        }

        /// <summary>
        /// Adds JWT authentication and authorization policies for EasyReasy.
        /// Optionally registers the IJwtTokenService for dependency injection.
        /// </summary>
        /// <param name="services">The service collection to add authentication to.</param>
        /// <param name="jwtSecret">The secret key used to sign JWT tokens.</param>
        /// <param name="issuer">The expected issuer for JWT tokens. If null, issuer validation is disabled.</param>
        /// <param name="registerJwtTokenService">Whether to automatically register IJwtTokenService. Default is true.</param>
        /// <param name="clockSkew">
        /// Optional clock skew tolerance for token lifetime validation. Defaults to 30 seconds.
        /// The <c>Microsoft.IdentityModel</c> default is 5 minutes, which is often too generous.
        /// Increase this if you see tokens rejected due to clock drift between servers.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEasyReasyAuth(
            this IServiceCollection services,
            string jwtSecret,
            string? issuer = null,
            bool registerJwtTokenService = true,
            TimeSpan? clockSkew = null)
        {
            byte[] key = Encoding.UTF8.GetBytes(jwtSecret);
            TimeSpan effectiveClockSkew = clockSkew ?? DefaultClockSkew;

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = issuer != null,
                    ValidIssuer = issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = effectiveClockSkew,
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiKeyOnly", policy =>
                    policy.RequireClaim("auth_type", "apikey"));
                options.AddPolicy("UserOnly", policy =>
                    policy.RequireClaim("auth_type", "user"));
            });

            // Register JWT token service for dependency injection if requested
            if (registerJwtTokenService)
            {
                services.AddSingleton<IJwtTokenService>(provider => new JwtTokenService(jwtSecret, issuer));
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
                return new RefreshTokenService(store, refreshTokenLifetime, accessTokenLifetime);
            });

            return services;
        }
    }
}