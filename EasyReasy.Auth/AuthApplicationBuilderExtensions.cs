using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Extension methods for configuring the EasyReasy authentication middleware pipeline.
    /// </summary>
    public static class AuthApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds authentication, authorization, claims injection, and (optionally) progressive delay middleware to the application pipeline.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="enableProgressiveDelay">If true, enables progressive delay for repeated unauthorized requests. Default is true.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseEasyReasyAuth(this IApplicationBuilder app, bool enableProgressiveDelay = true)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<ClaimsInjectionMiddleware>();
            if (enableProgressiveDelay)
                app.UseMiddleware<ProgressiveDelayMiddleware>();
            return app;
        }

        /// <summary>
        /// Adds an API key authentication endpoint to the application.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="validationService">The validation service for API key authentication.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddApiAuthEndpoint(
            this WebApplication app, 
            IAuthRequestValidationService validationService)
        {
            app.MapPost("/api/auth/apikey", async (ApiKeyAuthRequest request, IJwtTokenService jwtTokenService) =>
            {
                AuthResponse? response = await validationService.ValidateApiKeyRequestAsync(request, jwtTokenService);
                return response != null ? Results.Ok(response) : Results.Unauthorized();
            });

            return app;
        }

        /// <summary>
        /// Adds a username/password authentication endpoint to the application.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="validationService">The validation service for username/password authentication.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddLoginAuthEndpoint(
            this WebApplication app, 
            IAuthRequestValidationService validationService)
        {
            app.MapPost("/api/auth/login", async (LoginAuthRequest request, IJwtTokenService jwtTokenService) =>
            {
                AuthResponse? response = await validationService.ValidateLoginRequestAsync(request, jwtTokenService);
                return response != null ? Results.Ok(response) : Results.Unauthorized();
            });

            return app;
        }

        /// <summary>
        /// Adds authentication endpoints to the application based on the specified options.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <param name="validationService">The validation service for authentication.</param>
        /// <param name="allowApiKeys">Whether to enable API key authentication. Default is true.</param>
        /// <param name="allowUsernamePassword">Whether to enable username/password authentication. Default is true.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddAuthEndpoints(
            this WebApplication app, 
            IAuthRequestValidationService validationService,
            bool allowApiKeys = true,
            bool allowUsernamePassword = true)
        {
            if (allowApiKeys)
            {
                app.AddApiAuthEndpoint(validationService);
            }

            if (allowUsernamePassword)
            {
                app.AddLoginAuthEndpoint(validationService);
            }

            return app;
        }
    }
}