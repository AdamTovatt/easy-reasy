using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Extension methods for mapping the OAuth 2.1 authorization server's endpoints.
    /// </summary>
    public static class OAuthServerApplicationBuilderExtensions
    {
        /// <summary>
        /// Maps the authorization server's endpoints (paths in <see cref="OAuthServerPaths"/>): both metadata documents,
        /// registration (when enabled), authorization, token and revocation, all anonymous, and the consent page's request
        /// endpoints, which require a person signed in to the application itself: a token with auth type <c>user</c> and
        /// no <see cref="EasyReasyClaimTypes.ClientId"/> claim, so neither a client's token nor an API key's can approve a
        /// connection (403). Every response is <c>no-store</c>.
        /// </summary>
        /// <param name="app">The application.</param>
        /// <returns>The application for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The server, its store, its grant handler or EasyReasy.Auth's <see cref="IJwtTokenService"/> is not registered;
        /// every missing service is reported in one exception.
        /// </exception>
        public static WebApplication AddOAuthServerEndpoints(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);
            ThrowIfServicesMissing(app.Services);

            OAuthServerSettings settings = app.Services.GetRequiredService<OAuthServerSettings>();

            app.MapGet(OAuthServerPaths.AuthorizationServerMetadata, () => OAuthMetadataDocuments.AuthorizationServer(settings))
                .AllowAnonymous();
            app.MapGet(settings.ProtectedResourceMetadataPath, () => OAuthMetadataDocuments.ProtectedResource(settings))
                .AllowAnonymous();

            if (settings.AllowDynamicClientRegistration)
            {
                app.MapPost(OAuthServerPaths.Register, (OAuthRegistrationEndpoint endpoint, HttpContext httpContext) => endpoint.HandleAsync(httpContext))
                    .AllowAnonymous();
            }

            app.MapGet(OAuthServerPaths.Authorize, (OAuthAuthorizationEndpoint endpoint, HttpContext httpContext) => endpoint.AuthorizeAsync(httpContext))
                .AllowAnonymous();
            app.MapPost(OAuthServerPaths.Token, (OAuthTokenEndpoint endpoint, HttpContext httpContext) => endpoint.TokenAsync(httpContext))
                .AllowAnonymous();
            app.MapPost(OAuthServerPaths.Revoke, (OAuthRevocationEndpoint endpoint, HttpContext httpContext) => endpoint.RevokeAsync(httpContext))
                .AllowAnonymous();

            AuthorizationPolicy consentPolicy = OAuthConsentPolicy.Build();
            string requestRoute = OAuthServerPaths.AuthorizationRequestsPrefix + "/{request}";
            app.MapGet(requestRoute, (string request, OAuthAuthorizationEndpoint endpoint, HttpContext httpContext) => endpoint.GetRequestAsync(request, httpContext))
                .RequireAuthorization(consentPolicy);
            app.MapPost(requestRoute + "/approve", (string request, OAuthAuthorizationEndpoint endpoint, HttpContext httpContext) => endpoint.ApproveAsync(request, httpContext))
                .RequireAuthorization(consentPolicy);
            app.MapPost(requestRoute + "/deny", (string request, OAuthAuthorizationEndpoint endpoint, HttpContext httpContext) => endpoint.DenyAsync(request, httpContext))
                .RequireAuthorization(consentPolicy);

            return app;
        }

        private static void ThrowIfServicesMissing(IServiceProvider services)
        {
            IServiceProviderIsService serviceCheck = services.GetRequiredService<IServiceProviderIsService>();
            List<string> errors = new List<string>();

            if (!serviceCheck.IsService(typeof(OAuthServerSettings)))
            {
                errors.Add($"The OAuth server is not registered. Call builder.Services.AddEasyReasyOAuthServer<TStore, THandler>(...) before {nameof(AddOAuthServerEndpoints)}.");
            }

            if (!serviceCheck.IsService(typeof(IJwtTokenService)))
            {
                errors.Add($"{nameof(IJwtTokenService)} is not registered. Call builder.Services.AddEasyReasyAuth(...) before {nameof(AddOAuthServerEndpoints)}.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }
    }
}
