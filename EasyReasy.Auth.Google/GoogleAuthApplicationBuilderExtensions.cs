using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Extension methods for adding Google authentication endpoints to the application.
    /// </summary>
    public static class GoogleAuthApplicationBuilderExtensions
    {
        private const string NoCacheHeaderValue = "no-store";

        /// <summary>
        /// Adds a Google authentication endpoint at <c>POST /api/auth/google</c>.
        /// Expects a JSON body with an <c>idToken</c> field containing the Google ID token.
        /// </summary>
        /// <remarks>
        /// The endpoint is anonymous (so it stays reachable when the application applies a global authorization
        /// policy) and sets <c>Cache-Control: no-store</c> so the issued token is never cached, matching the
        /// built-in EasyReasy.Auth endpoints. If an <see cref="IAuthAuditLogger"/> is registered, its
        /// <see cref="IAuthAuditLogger.OnExternalAuthAsync"/> hook is invoked after the attempt (success or
        /// failure) before the response is written; an exception thrown by the logger propagates as a 500.
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown at startup if <see cref="IGoogleIdTokenValidator"/> (registered by <c>AddEasyReasyGoogleAuth</c>),
        /// the consumer-supplied <see cref="IGoogleAuthHandler"/>, or <see cref="IJwtTokenService"/> (registered by
        /// <c>AddEasyReasyAuth</c>) is missing from the DI container, so a misconfiguration fails fast instead of
        /// as a per-request 500.
        /// </exception>
        public static WebApplication AddGoogleAuthEndpoint(this WebApplication app)
        {
            EnsureRequiredServicesRegistered(app);

            app.MapPost("/api/auth/google", async (GoogleAuthRequest request, IJwtTokenService jwtTokenService, GoogleAuthService googleAuthService, HttpContext httpContext) =>
            {
                httpContext.Response.Headers["Cache-Control"] = NoCacheHeaderValue;

                IRefreshTokenService? refreshTokenService = httpContext.RequestServices.GetService<IRefreshTokenService>();

                ExternalAuthResult result = await googleAuthService.AuthenticateAsync(
                    request.IdToken,
                    jwtTokenService,
                    refreshTokenService,
                    httpContext);

                IAuthAuditLogger? auditLogger = httpContext.RequestServices.GetService<IAuthAuditLogger>();
                if (auditLogger != null)
                {
                    await auditLogger.OnExternalAuthAsync(httpContext, result);
                }

                return result.Success && result.AuthResponse != null
                    ? Results.Ok(result.AuthResponse)
                    : Results.Unauthorized();
            }).AllowAnonymous();

            return app;
        }

        /// <summary>
        /// Verifies, without instantiating them, that the services the Google endpoint depends on are
        /// registered, so a misconfiguration surfaces as a clear startup error rather than a per-request 500.
        /// </summary>
        private static void EnsureRequiredServicesRegistered(WebApplication app)
        {
            IServiceProviderIsService serviceCheck = app.Services.GetRequiredService<IServiceProviderIsService>();

            if (!serviceCheck.IsService(typeof(IGoogleIdTokenValidator)))
            {
                throw new InvalidOperationException(
                    $"{nameof(IGoogleIdTokenValidator)} is not registered in the DI container. " +
                    $"Call builder.Services.AddEasyReasyGoogleAuth(...) before {nameof(AddGoogleAuthEndpoint)}.");
            }

            if (!serviceCheck.IsService(typeof(IGoogleAuthHandler)))
            {
                throw new InvalidOperationException(
                    $"{nameof(IGoogleAuthHandler)} is not registered in the DI container. " +
                    $"Register your handler before {nameof(AddGoogleAuthEndpoint)}, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(IGoogleAuthHandler)}, MyGoogleAuthHandler>();");
            }

            if (!serviceCheck.IsService(typeof(IJwtTokenService)))
            {
                throw new InvalidOperationException(
                    $"{nameof(IJwtTokenService)} is not registered in the DI container. " +
                    $"Call builder.Services.AddEasyReasyAuth(...) before {nameof(AddGoogleAuthEndpoint)}.");
            }
        }
    }
}
