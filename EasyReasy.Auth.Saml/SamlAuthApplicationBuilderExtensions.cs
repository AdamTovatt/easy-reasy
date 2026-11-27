using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Extension methods for adding the SAML assertion consumer service and sign-in endpoints to the application.
    /// </summary>
    public static class SamlAuthApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds the SAML endpoints.
        /// <list type="bullet">
        /// <item><see cref="SamlAuthPaths.AssertionConsumerService"/> (<c>POST /api/auth/saml/acs</c>): takes the
        /// identity provider's form post, checks the response, keeps the outcome under a single-use code and answers 303 to
        /// the sign-in's completion path with <c>?code=</c>. It answers 400, with no code, when the post is not a form or
        /// has no <c>SAMLResponse</c>, when that is not base64 XML or names no <c>InResponseTo</c>, and when it answers no
        /// pending request this server began (unknown, already answered or expired): then there is no sign-in to send the
        /// browser back to. It serves every sign-in, the application's own flows through <see cref="ISamlAuthService"/>
        /// included.</item>
        /// <item><see cref="SamlAuthPaths.Begin"/> (<c>POST /api/auth/saml/begin</c>) takes a JSON body with the <c>identityProviderKey</c> the person chose,
        /// sets the sign-in's binding cookie and returns the <c>redirectUrl</c> to send the browser to, or 400 when the
        /// handler does not offer the key for sign-in or it resolves to no identity provider.</item>
        /// <item><see cref="SamlAuthPaths.SignIn"/> (<c>POST /api/auth/saml</c>) takes a JSON body with the <c>code</c> the completion page received, and
        /// answers like the other sign-in endpoints: 200 with the <see cref="AuthResponse"/>, or 401.</item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// All three are anonymous (so they stay reachable when the application applies a global authorization policy)
        /// and set <c>Cache-Control: no-store</c>. The assertion consumer service is a cross-site form post by design, so
        /// it opts out of antiforgery; what stands in for it is the response answering a request this server began, once.
        /// The completion page, the <c>ForceAuthn</c>, max authentication age and accepted classes of a sign-in come from
        /// <see cref="ISamlAuthHandler.GetSignInSettingsAsync"/>, never from the browser, which chooses only the identity
        /// provider.
        /// <para>
        /// The sign-in endpoint accepts only a code from a sign-in its own begin endpoint started: one an application began
        /// for its own flow is refused as <see cref="SamlAuthFailureReason.CallerBindingMismatch"/>. The rest of the
        /// sign-in endpoint is <see cref="ExternalSignInEndpoint.HandleAsync"/>, shared with every provider package: the
        /// audit record (here with the <see cref="SamlAuthFailureReason"/> name as
        /// <see cref="ExternalAuthResult.FailureDetail"/> and the account as <see cref="SamlIdentity.ToString"/>), the
        /// access-token cookie on success, the progressive-delay reset, and 200 or 401.
        /// </para>
        /// </remarks>
        /// <param name="app">The web application.</param>
        /// <returns>The mapped endpoints, one builder each, for adding a rate limiting policy or other metadata to one.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown at startup if <see cref="ISamlAuthService"/> (registered by <c>AddEasyReasySamlAuth</c>), the
        /// consumer-supplied <see cref="ISamlIdentityProviderResolver"/>, <see cref="ISamlAuthStore"/> or
        /// <see cref="ISamlAuthHandler"/>, or <see cref="IJwtTokenService"/> (registered by <c>AddEasyReasyAuth</c>) is
        /// missing from the DI container, so a misconfiguration fails fast instead of as a per-request 500.
        /// </exception>
        public static SamlAuthEndpoints AddSamlAuthEndpoints(this WebApplication app)
        {
            EnsureRequiredServicesRegistered(app);

            RouteHandlerBuilder assertionConsumerService = app.MapPost(SamlAuthPaths.AssertionConsumerService, async (SamlAuthService authService, HttpContext httpContext) =>
            {
                httpContext.SetNoStoreCacheControl();

                if (!httpContext.Request.HasFormContentType)
                {
                    return Results.BadRequest();
                }

                IFormCollection form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
                string? samlResponse = form["SAMLResponse"];
                if (string.IsNullOrEmpty(samlResponse))
                {
                    return Results.BadRequest();
                }

                string? completionLocation = await authService.ConsumeResponseAsync(samlResponse, httpContext.RequestAborted);
                if (completionLocation == null)
                {
                    return Results.BadRequest();
                }

                // 303, so the browser follows with a GET to the application's own page, which the Lax binding cookie
                // reaches once the page posts the code same-site.
                httpContext.Response.Headers.Location = completionLocation;
                return Results.StatusCode(StatusCodes.Status303SeeOther);
            }).AllowAnonymous().DisableAntiforgery();

            RouteHandlerBuilder begin = app.MapPost(SamlAuthPaths.Begin, async (
                SamlSignInBeginRequest request,
                ISamlAuthService authService,
                ISamlAuthHandler handler,
                HttpContext httpContext) =>
            {
                httpContext.SetNoStoreCacheControl();

                SamlSignInService signInService = new SamlSignInService(authService, handler);
                SamlSignInBeginResponse? response = await signInService.BeginAsync(request.IdentityProviderKey, httpContext);
                return response == null ? Results.BadRequest() : Results.Ok(response);
            }).AllowAnonymous();

            RouteHandlerBuilder signIn = app.MapPost(SamlAuthPaths.SignIn, (
                SamlSignInRequest request,
                ISamlAuthService authService,
                ISamlAuthHandler handler,
                HttpContext httpContext) =>
            {
                SamlSignInService signInService = new SamlSignInService(authService, handler);
                return ExternalSignInEndpoint.HandleAsync(httpContext, (jwtTokenService, refreshTokenService) =>
                    signInService.AuthenticateAsync(request.Code, jwtTokenService, refreshTokenService, httpContext));
            }).AllowAnonymous();

            return new SamlAuthEndpoints
            {
                AssertionConsumerService = assertionConsumerService,
                Begin = begin,
                SignIn = signIn,
            };
        }

        /// <summary>
        /// Verifies, without instantiating them, that the services the endpoints depend on are registered, so a
        /// misconfiguration surfaces as a clear startup error rather than a per-request 500.
        /// </summary>
        private static void EnsureRequiredServicesRegistered(WebApplication app)
        {
            IServiceProviderIsService serviceCheck = app.Services.GetRequiredService<IServiceProviderIsService>();
            List<string> errors = new List<string>();

            if (!serviceCheck.IsService(typeof(ISamlAuthService)))
            {
                errors.Add(
                    $"{nameof(ISamlAuthService)} is not registered in the DI container. " +
                    $"Call builder.Services.AddEasyReasySamlAuth(...) before {nameof(AddSamlAuthEndpoints)}.");
            }

            if (!serviceCheck.IsService(typeof(ISamlIdentityProviderResolver)))
            {
                errors.Add(
                    $"{nameof(ISamlIdentityProviderResolver)} is not registered in the DI container. " +
                    $"Register your resolver before {nameof(AddSamlAuthEndpoints)}, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(ISamlIdentityProviderResolver)}, MyIdentityProviderResolver>();");
            }

            if (!serviceCheck.IsService(typeof(ISamlAuthStore)))
            {
                errors.Add(
                    $"{nameof(ISamlAuthStore)} is not registered in the DI container. " +
                    $"Register your store before {nameof(AddSamlAuthEndpoints)}, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(ISamlAuthStore)}, MySamlAuthStore>();");
            }

            if (!serviceCheck.IsService(typeof(ISamlAuthHandler)))
            {
                errors.Add(
                    $"{nameof(ISamlAuthHandler)} is not registered in the DI container. " +
                    $"Register your handler before {nameof(AddSamlAuthEndpoints)}, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(ISamlAuthHandler)}, MySamlAuthHandler>();");
            }

            errors.AddRange(ExternalSignInEndpoint.FindMissingServices(app.Services, nameof(AddSamlAuthEndpoints)));

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }
    }
}
