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
        private const string NoCacheHeaderValue = "no-store";

        /// <summary>
        /// Adds authentication, authorization, claims injection, and progressive delay middleware
        /// to the application pipeline using default options.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseEasyReasyAuth(this IApplicationBuilder app)
        {
            return UseEasyReasyAuth(app, configure: null);
        }

        /// <summary>
        /// Adds authentication, authorization, claims injection, and (optionally) progressive delay middleware
        /// to the application pipeline with the specified configuration.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="configure">An optional action to configure <see cref="ProgressiveDelayOptions"/>.</param>
        /// <returns>The application builder for chaining.</returns>
        public static IApplicationBuilder UseEasyReasyAuth(
            this IApplicationBuilder app,
            Action<ProgressiveDelayOptions>? configure)
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions();
            configure?.Invoke(options);

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<ClaimsInjectionMiddleware>();

            if (options.Enabled)
            {
                app.UseMiddleware<ProgressiveDelayMiddleware>(options);
            }

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
                httpContext.Response.Headers["Cache-Control"] = NoCacheHeaderValue;
                AuthResponse? response = await validationService.ValidateApiKeyRequestAsync(request, jwtTokenService, httpContext);
                return response != null ? Results.Ok(response) : Results.Unauthorized();
            }).AllowAnonymous();

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
                httpContext.Response.Headers["Cache-Control"] = NoCacheHeaderValue;
                AuthResponse? response = await validationService.ValidateLoginRequestAsync(request, jwtTokenService, httpContext);
                return response != null ? Results.Ok(response) : Results.Unauthorized();
            }).AllowAnonymous();

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
            app.MapPost("/api/auth/refresh", async (RefreshRequest request, IRefreshTokenService refreshTokenService, IJwtTokenService jwtTokenService, HttpContext httpContext) =>
            {
                httpContext.Response.Headers["Cache-Control"] = NoCacheHeaderValue;
                RefreshResult result = await refreshTokenService.RefreshAsync(request.RefreshToken, jwtTokenService, httpContext.RequestAborted);

                if (result.Success)
                {
                    return Results.Ok(result.AuthResponse);
                }

                return Results.Unauthorized();
            }).AllowAnonymous();

            return app;
        }

        /// <summary>
        /// Adds a logout endpoint that revokes the refresh token family for the supplied token.
        /// Requires <see cref="IRefreshTokenService"/> to be registered in DI.
        /// </summary>
        /// <remarks>
        /// The endpoint is anonymous (no access token required), accepts a <see cref="LogoutRequest"/> body,
        /// and always returns 204 No Content — even when the token is unknown, null, or already invalidated —
        /// so that the response body does not reveal whether the supplied token was known.
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddLogoutEndpoint(this WebApplication app)
        {
            app.MapPost("/api/auth/logout", async (LogoutRequest request, IRefreshTokenService refreshTokenService, HttpContext httpContext) =>
            {
                httpContext.Response.Headers["Cache-Control"] = NoCacheHeaderValue;
                await refreshTokenService.LogoutAsync(request.RefreshToken, httpContext.RequestAborted);
                return Results.NoContent();
            }).AllowAnonymous();

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
        /// <param name="allowRefresh">Whether to enable the refresh token endpoint. Default is false (opt-in because refresh changes how access tokens are issued).</param>
        /// <param name="allowLogout">Whether to enable the logout endpoint. Default is true (safe to expose because logout is a no-op for unknown tokens and gives consumers the full logout story out of the box).</param>
        /// <returns>The web application for chaining.</returns>
        public static WebApplication AddAuthEndpoints(
            this WebApplication app,
            bool allowApiKeys = true,
            bool allowUsernamePassword = true,
            bool allowRefresh = false,
            bool allowLogout = true)
        {
            // Fail fast at startup if the required services are not registered
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

                if (allowRefresh || allowLogout)
                {
                    IRefreshTokenService? refreshTokenService = scope.ServiceProvider.GetService<IRefreshTokenService>();
                    if (refreshTokenService == null)
                    {
                        throw new InvalidOperationException(
                            $"{nameof(IRefreshTokenService)} is not registered in the DI container, " +
                            $"but the refresh or logout endpoint is enabled. " +
                            $"Register it before calling {nameof(AddAuthEndpoints)}, e.g.: " +
                            $"builder.Services.AddRefreshTokenService<MyRefreshTokenStore>(); " +
                            $"or disable the endpoints with allowRefresh: false and allowLogout: false.");
                    }
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

            if (allowLogout)
            {
                app.AddLogoutEndpoint();
            }

            return app;
        }
    }
}
