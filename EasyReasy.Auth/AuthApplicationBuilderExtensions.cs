using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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
        /// <remarks>
        /// Requires <see cref="IAuthRequestValidationService"/> to be registered in DI.
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddApiAuthEndpoint(this WebApplication app)
        {
            app.MapPost("/api/auth/apikey", async (ApiKeyAuthRequest request, IAuthRequestValidationService validationService, IJwtTokenService jwtTokenService, HttpContext httpContext) =>
            {
                AuthResponse? response = await validationService.ValidateApiKeyRequestAsync(request, jwtTokenService, httpContext);
                return response != null ? Results.Ok(response) : Results.Unauthorized();
            });

            return app;
        }

        /// <summary>
        /// Adds a username/password authentication endpoint to the application.
        /// </summary>
        /// <remarks>
        /// Requires <see cref="IAuthRequestValidationService"/> to be registered in DI.
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddLoginAuthEndpoint(this WebApplication app)
        {
            app.MapPost("/api/auth/login", async (LoginAuthRequest request, IAuthRequestValidationService validationService, IJwtTokenService jwtTokenService, HttpContext httpContext) =>
            {
                AuthResponse? response = await validationService.ValidateLoginRequestAsync(request, jwtTokenService, httpContext);
                return response != null ? Results.Ok(response) : Results.Unauthorized();
            });

            return app;
        }

        /// <summary>
        /// Adds a refresh token endpoint to the application.
        /// Requires <see cref="IRefreshTokenService"/> and <see cref="IJwtTokenService"/> to be registered in DI.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddRefreshEndpoint(this WebApplication app)
        {
            app.MapPost("/api/auth/refresh", async (RefreshRequest request, IRefreshTokenService refreshTokenService, IJwtTokenService jwtTokenService) =>
            {
                RefreshResult result = await refreshTokenService.RefreshAsync(request.RefreshToken, jwtTokenService);

                if (result.Success)
                {
                    return Results.Ok(result.AuthResponse);
                }

                return Results.Unauthorized();
            });

            return app;
        }

        /// <summary>
        /// Adds authentication endpoints to the application based on the specified options.
        /// </summary>
        /// <remarks>
        /// Requires <see cref="IAuthRequestValidationService"/> to be registered in DI.
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <param name="allowApiKeys">Whether to enable API key authentication. Default is true.</param>
        /// <param name="allowUsernamePassword">Whether to enable username/password authentication. Default is true.</param>
        /// <param name="allowRefresh">Whether to enable the refresh token endpoint. Default is false.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddAuthEndpoints(
            this WebApplication app,
            bool allowApiKeys = true,
            bool allowUsernamePassword = true,
            bool allowRefresh = false)
        {
            // Fail fast at startup if the required service is not registered
            using (IServiceScope scope = app.Services.CreateScope())
            {
                IAuthRequestValidationService? validationService = scope.ServiceProvider.GetService<IAuthRequestValidationService>();
                if (validationService == null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(IAuthRequestValidationService)} is not registered in the DI container. " +
                        $"Register it before calling {nameof(AddAuthEndpoints)}, e.g.: " +
                        $"builder.Services.AddScoped<{nameof(IAuthRequestValidationService)}, MyAuthService>();");
                }
            }

            if (allowApiKeys)
            {
                app.AddApiAuthEndpoint();
            }

            if (allowUsernamePassword)
            {
                app.AddLoginAuthEndpoint();
            }

            if (allowRefresh)
            {
                app.AddRefreshEndpoint();
            }

            return app;
        }
    }
}
