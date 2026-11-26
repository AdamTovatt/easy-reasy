using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Extension methods for adding the OpenID Connect sign-in endpoints to the application.
    /// </summary>
    public static class OpenIdConnectAuthApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds the two OpenID Connect sign-in endpoints. <c>POST /api/auth/oidc/begin</c> takes a JSON body with the
        /// <c>providerKey</c> the person chose and an optional <c>loginHint</c>, sets the flow's binding cookie and returns
        /// the <c>authorizationUrl</c> to send the browser to, or 400 when the key resolves to no provider.
        /// <c>POST /api/auth/oidc</c> takes a JSON body with the <c>code</c>, <c>state</c> and <c>iss</c> the provider's
        /// redirect brought back and the <c>redirectUri</c> it arrived at, and answers like the other sign-in endpoints:
        /// 200 with the <see cref="AuthResponse"/>, or 401.
        /// </summary>
        /// <remarks>
        /// Both endpoints are anonymous (so they stay reachable when the application applies a global authorization
        /// policy) and set <c>Cache-Control: no-store</c>, matching the built-in EasyReasy.Auth endpoints. The redirect URI,
        /// prompt, max authentication age and accepted <c>acr</c> and <c>amr</c> values of a sign-in come from
        /// <see cref="IOpenIdConnectAuthHandler.GetSignInSettingsAsync"/>, never from the browser, which chooses only the
        /// provider.
        /// <para>
        /// The sign-in endpoint accepts only a <c>state</c> its own begin endpoint issued: one an application began for its
        /// own flow through <see cref="IOpenIdConnectAuthService"/> is refused as
        /// <see cref="OpenIdConnectAuthFailureReason.CallerBindingMismatch"/>. The rest of the sign-in endpoint is
        /// <see cref="ExternalSignInEndpoint.HandleAsync"/>, shared with every provider package: the audit record (here
        /// with the <see cref="OpenIdConnectAuthFailureReason"/> name as <see cref="ExternalAuthResult.FailureDetail"/>
        /// and the account as <see cref="OpenIdConnectIdentity.ToString"/>), the access-token cookie on success, the
        /// progressive-delay reset, and 200 or 401. A provider's token endpoint being unreachable or answering with a
        /// status other than 400 or 401, or its discovery document or keys being unavailable, is not a refusal: it
        /// propagates as a 500 and is not audited.
        /// </para>
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <returns>The web application for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown at startup if <see cref="IOpenIdConnectAuthService"/> (registered by <c>AddEasyReasyOpenIdConnectAuth</c>),
        /// the consumer-supplied <see cref="IOpenIdConnectProviderResolver"/> or <see cref="IOpenIdConnectAuthHandler"/>, or
        /// <see cref="IJwtTokenService"/> (registered by <c>AddEasyReasyAuth</c>) is missing from the DI container, so a
        /// misconfiguration fails fast instead of as a per-request 500.
        /// </exception>
        public static WebApplication AddOpenIdConnectAuthEndpoints(this WebApplication app)
        {
            EnsureRequiredServicesRegistered(app);

            app.MapPost("/api/auth/oidc/begin", async (
                OpenIdConnectSignInBeginRequest request,
                [FromKeyedServices(OpenIdConnectAuthServiceCollectionExtensions.ApplicationFlowKey)] OpenIdConnectCodeFlow flow,
                IOpenIdConnectProviderResolver resolver,
                IOpenIdConnectAuthHandler handler,
                HttpContext httpContext) =>
            {
                httpContext.SetNoStoreCacheControl();

                OpenIdConnectSignInService signInService = new OpenIdConnectSignInService(flow, resolver, handler);
                OpenIdConnectSignInBeginResponse? response = await signInService.BeginAsync(request.ProviderKey, request.LoginHint, httpContext);
                return response == null ? Results.BadRequest() : Results.Ok(response);
            }).AllowAnonymous();

            app.MapPost("/api/auth/oidc", (
                OpenIdConnectAuthorizationResponse request,
                [FromKeyedServices(OpenIdConnectAuthServiceCollectionExtensions.ApplicationFlowKey)] OpenIdConnectCodeFlow flow,
                IOpenIdConnectProviderResolver resolver,
                IOpenIdConnectAuthHandler handler,
                HttpContext httpContext) =>
            {
                OpenIdConnectSignInService signInService = new OpenIdConnectSignInService(flow, resolver, handler);
                return ExternalSignInEndpoint.HandleAsync(httpContext, (jwtTokenService, refreshTokenService) =>
                    signInService.AuthenticateAsync(request, jwtTokenService, refreshTokenService, httpContext));
            }).AllowAnonymous();

            return app;
        }

        /// <summary>
        /// Verifies, without instantiating them, that the services the endpoints depend on are registered, so a
        /// misconfiguration surfaces as a clear startup error rather than a per-request 500.
        /// </summary>
        private static void EnsureRequiredServicesRegistered(WebApplication app)
        {
            IServiceProviderIsService serviceCheck = app.Services.GetRequiredService<IServiceProviderIsService>();
            List<string> errors = new List<string>();

            // The interface and the keyed flow the endpoints use are registered together by AddEasyReasyOpenIdConnectAuth.
            if (!serviceCheck.IsService(typeof(IOpenIdConnectAuthService)))
            {
                errors.Add(
                    $"{nameof(IOpenIdConnectAuthService)} is not registered in the DI container. " +
                    $"Call builder.Services.AddEasyReasyOpenIdConnectAuth(...) before {nameof(AddOpenIdConnectAuthEndpoints)}.");
            }

            if (!serviceCheck.IsService(typeof(IOpenIdConnectProviderResolver)))
            {
                errors.Add(
                    $"{nameof(IOpenIdConnectProviderResolver)} is not registered in the DI container. " +
                    $"Register your resolver before {nameof(AddOpenIdConnectAuthEndpoints)}, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(IOpenIdConnectProviderResolver)}, MyProviderResolver>();");
            }

            if (!serviceCheck.IsService(typeof(IOpenIdConnectAuthHandler)))
            {
                errors.Add(
                    $"{nameof(IOpenIdConnectAuthHandler)} is not registered in the DI container. " +
                    $"Register your handler before {nameof(AddOpenIdConnectAuthEndpoints)}, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(IOpenIdConnectAuthHandler)}, MyOpenIdConnectAuthHandler>();");
            }

            errors.AddRange(ExternalSignInEndpoint.FindMissingServices(app.Services, nameof(AddOpenIdConnectAuthEndpoints)));

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }
    }
}
