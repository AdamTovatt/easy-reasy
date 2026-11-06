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
        /// <summary>
        /// Adds a Google authentication endpoint at <c>POST /api/auth/google</c>.
        /// Expects a JSON body with an <c>idToken</c> field containing the Google ID token.
        /// </summary>
        /// <remarks>
        /// The endpoint is anonymous (so it stays reachable when the application applies a global authorization
        /// policy) and sets <c>Cache-Control: no-store</c> so the issued token is never cached, matching the
        /// built-in EasyReasy.Auth endpoints. The response is <see cref="ExternalSignInEndpoint.HandleAsync"/>, shared
        /// with every provider package: if an <see cref="IAuthAuditLogger"/> is registered, its
        /// <see cref="IAuthAuditLogger.OnExternalAuthAsync"/> hook is invoked after the attempt (success or failure)
        /// before the response is written, and an exception thrown by the logger propagates as a 500.
        /// <para>
        /// When <see cref="EasyReasyAuthOptions.CookieTokenPaths"/> is non-empty, a successful sign-in also sets the
        /// access-token cookie through <see cref="HttpContextExtensions.TrySetAccessTokenCookie"/>, as the built-in
        /// login endpoint does. A rejected sign-in sets none, and the JSON response is unchanged.
        /// </para>
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown at startup if <see cref="IGoogleIdTokenValidator"/> (registered by <c>AddEasyReasyGoogleAuth</c>),
        /// the consumer-supplied <see cref="IGoogleAuthHandler"/>, or <see cref="IJwtTokenService"/> (registered by
        /// <c>AddEasyReasyAuth</c>) is missing from the DI container, so a misconfiguration fails fast instead of
        /// as a per-request 500. Every missing service is named in the one exception.
        /// </exception>
        public static WebApplication AddGoogleAuthEndpoint(this WebApplication app)
        {
            EnsureRequiredServicesRegistered(app);

            app.MapPost("/api/auth/google", (GoogleAuthRequest request, GoogleAuthService googleAuthService, HttpContext httpContext) =>
            {
                return ExternalSignInEndpoint.HandleAsync(httpContext, (jwtTokenService, refreshTokenService) =>
                    googleAuthService.AuthenticateAsync(request.IdToken, jwtTokenService, refreshTokenService, httpContext));
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
            List<string> errors = new List<string>();

            if (!serviceCheck.IsService(typeof(IGoogleIdTokenValidator)))
            {
                errors.Add(
                    $"{nameof(IGoogleIdTokenValidator)} is not registered in the DI container. " +
                    $"Call builder.Services.AddEasyReasyGoogleAuth(...) before {nameof(AddGoogleAuthEndpoint)}.");
            }

            if (!serviceCheck.IsService(typeof(IGoogleAuthHandler)))
            {
                errors.Add(
                    $"{nameof(IGoogleAuthHandler)} is not registered in the DI container. " +
                    $"Register your handler before {nameof(AddGoogleAuthEndpoint)}, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(IGoogleAuthHandler)}, MyGoogleAuthHandler>();");
            }

            errors.AddRange(ExternalSignInEndpoint.FindMissingServices(app.Services, nameof(AddGoogleAuthEndpoint)));

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }
    }
}
