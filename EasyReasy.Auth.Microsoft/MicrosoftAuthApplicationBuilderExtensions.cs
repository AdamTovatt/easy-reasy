using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Extension methods for adding the Microsoft sign-in endpoints to the application.
    /// </summary>
    public static class MicrosoftAuthApplicationBuilderExtensions
    {
        private const string NoCacheHeaderValue = "no-store";

        /// <summary>
        /// Adds the two Microsoft sign-in endpoints. <c>POST /api/auth/microsoft/begin</c> takes an optional JSON body
        /// with a <c>loginHint</c>, sets the flow's binding cookie and returns the <c>authorizationUrl</c> to send the
        /// browser to. <c>POST /api/auth/microsoft</c> takes a JSON body with the <c>code</c> and <c>state</c>
        /// Microsoft's redirect brought back, and answers like the Google sign-in endpoint: 200 with the
        /// <see cref="AuthResponse"/>, or 401.
        /// </summary>
        /// <remarks>
        /// Both endpoints are anonymous (so they stay reachable when the application applies a global authorization
        /// policy) and set <c>Cache-Control: no-store</c>, matching the built-in EasyReasy.Auth endpoints. The tenant,
        /// prompt, authentication context, max authentication age and redirect URI of a sign-in come from
        /// <see cref="IMicrosoftAuthHandler.GetSignInSettingsAsync"/>, never from the browser.
        /// <para>
        /// The sign-in endpoint accepts only a <c>state</c> its own begin endpoint issued: one an application began
        /// for its own flow through <see cref="IMicrosoftAuthService"/> is refused as
        /// <see cref="MicrosoftAuthFailureReason.CallerBindingMismatch"/>. The rest of the sign-in endpoint is
        /// <see cref="ExternalSignInEndpoint.HandleAsync"/>, shared with every provider package: the audit record
        /// (here with the <see cref="MicrosoftAuthFailureReason"/> name as <see cref="ExternalAuthResult.FailureDetail"/>),
        /// the access-token cookie on success, the progressive-delay reset, and 200 or 401. Microsoft's token endpoint
        /// or signing keys being unreachable is not a refusal: it propagates as a 500 and is not audited.
        /// </para>
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown at startup if <see cref="IMicrosoftAuthService"/> (registered by <c>AddEasyReasyMicrosoftAuth</c>),
        /// the consumer-supplied <see cref="IMicrosoftAuthHandler"/>, or <see cref="IJwtTokenService"/> (registered by
        /// <c>AddEasyReasyAuth</c>) is missing from the DI container, so a misconfiguration fails fast instead of as a
        /// per-request 500.
        /// </exception>
        public static WebApplication AddMicrosoftAuthEndpoints(this WebApplication app)
        {
            EnsureRequiredServicesRegistered(app);

            app.MapPost("/api/auth/microsoft/begin", async (
                [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MicrosoftSignInBeginRequest? request,
                IMicrosoftAuthService authService,
                IMicrosoftAuthHandler handler,
                HttpContext httpContext) =>
            {
                httpContext.Response.Headers["Cache-Control"] = NoCacheHeaderValue;

                MicrosoftSignInService signInService = new MicrosoftSignInService(authService, handler);
                MicrosoftSignInBeginResponse response = await signInService.BeginAsync(request?.LoginHint, httpContext);
                return Results.Ok(response);
            }).AllowAnonymous();

            app.MapPost("/api/auth/microsoft", (
                MicrosoftSignInRequest request,
                IMicrosoftAuthService authService,
                IMicrosoftAuthHandler handler,
                HttpContext httpContext) =>
            {
                MicrosoftSignInService signInService = new MicrosoftSignInService(authService, handler);
                return ExternalSignInEndpoint.HandleAsync(httpContext, (jwtTokenService, refreshTokenService) =>
                    signInService.AuthenticateAsync(request.Code, request.State, jwtTokenService, refreshTokenService, httpContext));
            }).AllowAnonymous();

            return app;
        }

        /// <summary>
        /// Verifies, without instantiating them, that the services the Microsoft endpoints depend on are registered,
        /// so a misconfiguration surfaces as a clear startup error rather than a per-request 500.
        /// </summary>
        private static void EnsureRequiredServicesRegistered(WebApplication app)
        {
            IServiceProviderIsService serviceCheck = app.Services.GetRequiredService<IServiceProviderIsService>();
            List<string> errors = new List<string>();

            if (!serviceCheck.IsService(typeof(IMicrosoftAuthService)))
            {
                errors.Add(
                    $"{nameof(IMicrosoftAuthService)} is not registered in the DI container. " +
                    $"Call builder.Services.AddEasyReasyMicrosoftAuth(...) before {nameof(AddMicrosoftAuthEndpoints)}.");
            }

            if (!serviceCheck.IsService(typeof(IMicrosoftAuthHandler)))
            {
                errors.Add(
                    $"{nameof(IMicrosoftAuthHandler)} is not registered in the DI container. " +
                    $"Register your handler before {nameof(AddMicrosoftAuthEndpoints)}, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(IMicrosoftAuthHandler)}, MyMicrosoftAuthHandler>();");
            }

            if (!serviceCheck.IsService(typeof(IJwtTokenService)))
            {
                errors.Add(
                    $"{nameof(IJwtTokenService)} is not registered in the DI container. " +
                    $"Call builder.Services.AddEasyReasyAuth(...) before {nameof(AddMicrosoftAuthEndpoints)}.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }
    }
}
