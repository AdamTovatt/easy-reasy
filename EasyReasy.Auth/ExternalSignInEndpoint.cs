using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The part every identity-provider sign-in endpoint shares, so a provider package (such as
    /// <c>EasyReasy.Auth.Google</c>) supplies only how it authenticates the caller, and the response, the audit record,
    /// the access-token cookie and the progressive-delay reset happen the same way, in the same order, for every
    /// provider.
    /// </summary>
    public static class ExternalSignInEndpoint
    {
        /// <summary>
        /// Finds, without instantiating anything, the services <see cref="HandleAsync"/> needs that are not
        /// registered, for a provider package's startup check to report along with its own.
        /// </summary>
        /// <param name="services">The application's root service provider.</param>
        /// <param name="mapMethodName">The provider's endpoint-mapping method, named in each message (for example
        /// <c>AddGoogleAuthEndpoint</c>).</param>
        /// <returns>One message per missing service; empty when all are registered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="mapMethodName"/> is blank.</exception>
        public static IReadOnlyList<string> FindMissingServices(IServiceProvider services, string mapMethodName)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(mapMethodName);

            IServiceProviderIsService serviceCheck = services.GetRequiredService<IServiceProviderIsService>();
            List<string> errors = new List<string>();

            if (!serviceCheck.IsService(typeof(IJwtTokenService)))
            {
                errors.Add(
                    $"{nameof(IJwtTokenService)} is not registered in the DI container. " +
                    $"Call builder.Services.AddEasyReasyAuth(...) before {mapMethodName}.");
            }

            return errors;
        }

        /// <summary>
        /// Runs a sign-in endpoint's request: sets <c>Cache-Control: no-store</c>, calls
        /// <paramref name="authenticate"/> with the registered <see cref="IJwtTokenService"/> and
        /// <see cref="IRefreshTokenService"/>, reports the result to <see cref="IAuthAuditLogger.OnExternalAuthAsync"/>
        /// if an <see cref="IAuthAuditLogger"/> is registered, and answers 200 with the <see cref="AuthResponse"/> or
        /// 401.
        /// </summary>
        /// <remarks>
        /// On success, and only then, it also sets the access-token cookie when
        /// <see cref="EasyReasyAuthOptions.CookieTokenPaths"/> is non-empty, through
        /// <see cref="HttpContextExtensions.TrySetAccessTokenCookie"/> as the built-in login endpoint does (a token too
        /// large for the cookie is logged and the sign-in still succeeds, since the session is already issued), and
        /// calls <see cref="HttpContextExtensions.MarkAuthenticationSucceeded"/> so
        /// <see cref="ProgressiveDelayMiddleware"/> clears the IP's failure count. The audit record is written before
        /// the response; an exception thrown by the audit logger or by <paramref name="authenticate"/> propagates as a
        /// 500.
        /// </remarks>
        /// <param name="httpContext">The sign-in request.</param>
        /// <param name="authenticate">
        /// The provider's own work: verify the caller's identity, hand it to the application's handler with the two
        /// services it is given (the refresh token service is null when none is registered), and return the outcome.
        /// </param>
        /// <returns>The endpoint's result.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException"><see cref="IJwtTokenService"/> is not registered.</exception>
        public static async Task<IResult> HandleAsync(
            HttpContext httpContext,
            Func<IJwtTokenService, IRefreshTokenService?, Task<ExternalAuthResult>> authenticate)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(authenticate);

            httpContext.SetNoStoreCacheControl();

            IJwtTokenService jwtTokenService = httpContext.RequestServices.GetRequiredService<IJwtTokenService>();
            IRefreshTokenService? refreshTokenService = httpContext.RequestServices.GetService<IRefreshTokenService>();

            ExternalAuthResult result = await authenticate(jwtTokenService, refreshTokenService);

            IAuthAuditLogger? auditLogger = httpContext.RequestServices.GetService<IAuthAuditLogger>();
            if (auditLogger != null)
            {
                await auditLogger.OnExternalAuthAsync(httpContext, result);
            }

            if (result.Success && result.AuthResponse != null)
            {
                if (httpContext.IsAccessTokenCookieEnabled())
                {
                    httpContext.TrySetAccessTokenCookie(result.AuthResponse.Token);
                }

                httpContext.MarkAuthenticationSucceeded();
                return Results.Ok(result.AuthResponse);
            }

            return Results.Unauthorized();
        }
    }
}
