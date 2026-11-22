using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
        /// no <see cref="EasyReasyClaimTypes.ClientId"/> claim, so neither a client's token (401, as these endpoints are not
        /// open to connections) nor an API key's (403) can approve a connection. Every response is <c>no-store</c>.
        /// </summary>
        /// <param name="app">The application.</param>
        /// <returns>
        /// Each mapped endpoint's builder, so the application can add its own metadata to one endpoint (a rate limiting
        /// policy, say) without matching its path.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// The server, its store, its grant handler or EasyReasy.Auth itself is not registered;
        /// every missing service is reported in one exception.
        /// </exception>
        public static OAuthServerEndpoints AddOAuthServerEndpoints(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);
            ThrowIfServicesMissing(app.Services);

            OAuthServerSettings settings = app.Services.GetRequiredService<OAuthServerSettings>();

            // The client-facing endpoints are anonymous: a client authenticates at the token and revocation endpoints by
            // its own credentials, never a bearer token.
            RouteGroupBuilder clientEndpoints = app.MapGroup("").AllowAnonymous();
            // The consent endpoints take only the app's own person token, by the consent policy.
            RouteGroupBuilder consentEndpoints = app.MapGroup(OAuthServerPaths.AuthorizationRequestsPrefix)
                .RequireAuthorization(OAuthConsentPolicy.Build());

            return new OAuthServerEndpoints
            {
                AuthorizationServerMetadata = clientEndpoints.MapGet(
                    OAuthServerPaths.AuthorizationServerMetadata, () => OAuthMetadataDocuments.AuthorizationServer(settings)),
                ProtectedResourceMetadata = clientEndpoints.MapGet(
                    settings.ProtectedResourceMetadataPath, () => OAuthMetadataDocuments.ProtectedResource(settings)),
                Registration = settings.AllowDynamicClientRegistration
                    ? clientEndpoints.MapPost(OAuthServerPaths.Register, (OAuthRegistrationEndpoint endpoint, HttpContext httpContext) => endpoint.HandleAsync(httpContext))
                    : null,
                Authorization = clientEndpoints.MapGet(
                    OAuthServerPaths.Authorize, (OAuthAuthorizationEndpoint endpoint, HttpContext httpContext) => endpoint.AuthorizeAsync(httpContext)),
                Token = clientEndpoints.MapPost(
                    OAuthServerPaths.Token, (OAuthTokenEndpoint endpoint, HttpContext httpContext) => endpoint.TokenAsync(httpContext)),
                Revocation = clientEndpoints.MapPost(
                    OAuthServerPaths.Revoke, (OAuthRevocationEndpoint endpoint, HttpContext httpContext) => endpoint.RevokeAsync(httpContext)),
                ConsentRequest = consentEndpoints.MapGet(
                    "/{request}", (string request, OAuthAuthorizationEndpoint endpoint, HttpContext httpContext) => endpoint.GetRequestAsync(request, httpContext)),
                ConsentApproval = consentEndpoints.MapPost(
                    "/{request}/approve", (string request, OAuthAuthorizationEndpoint endpoint, HttpContext httpContext) => endpoint.ApproveAsync(request, httpContext)),
                ConsentDenial = consentEndpoints.MapPost(
                    "/{request}/deny", (string request, OAuthAuthorizationEndpoint endpoint, HttpContext httpContext) => endpoint.DenyAsync(request, httpContext)),
            };
        }

        private static void ThrowIfServicesMissing(IServiceProvider services)
        {
            IServiceProviderIsService serviceCheck = services.GetRequiredService<IServiceProviderIsService>();
            List<string> errors = new List<string>();

            if (!serviceCheck.IsService(typeof(OAuthServerSettings)))
            {
                errors.Add($"The OAuth server is not registered. Call builder.Services.AddEasyReasyOAuthServer<TStore, THandler>(...) before {nameof(AddOAuthServerEndpoints)}.");
            }

            if (!serviceCheck.IsService(typeof(JwtSigningSettings)))
            {
                errors.Add($"EasyReasy.Auth is not registered. Call builder.Services.AddEasyReasyAuth(...) before {nameof(AddOAuthServerEndpoints)}.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
        }
    }
}
