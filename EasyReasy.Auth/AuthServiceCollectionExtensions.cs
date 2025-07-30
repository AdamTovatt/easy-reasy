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
        /// Also registers the IJwtTokenService for dependency injection.
        /// </summary>
        /// <param name="services">The service collection to add authentication to.</param>
        /// <param name="jwtSecret">The secret key used to sign JWT tokens.</param>
        /// <param name="issuer">The expected issuer for JWT tokens. If null, issuer validation is disabled.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEasyReasyAuth(
            this IServiceCollection services,
            string jwtSecret,
            string? issuer = null)
        {
            return AddEasyReasyAuth(services, jwtSecret, issuer, registerJwtTokenService: true);
        }

        /// <summary>
        /// Adds JWT authentication and authorization policies for EasyReasy.
        /// Optionally registers the IJwtTokenService for dependency injection.
        /// </summary>
        /// <param name="services">The service collection to add authentication to.</param>
        /// <param name="jwtSecret">The secret key used to sign JWT tokens.</param>
        /// <param name="issuer">The expected issuer for JWT tokens. If null, issuer validation is disabled.</param>
        /// <param name="registerJwtTokenService">Whether to automatically register IJwtTokenService. Default is true.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEasyReasyAuth(
            this IServiceCollection services,
            string jwtSecret,
            string? issuer = null,
            bool registerJwtTokenService = true)
        {
            byte[] key = Encoding.UTF8.GetBytes(jwtSecret);

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
        /// Registers an authentication request validation service for dependency injection.
        /// </summary>
        /// <param name="services">The service collection to add the validation service to.</param>
        /// <param name="validationService">The validation service instance.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddAuthValidationService(
            this IServiceCollection services, 
            IAuthRequestValidationService validationService)
        {
            services.AddSingleton(validationService);
            return services;
        }
    }
}