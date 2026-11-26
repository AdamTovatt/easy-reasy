using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The registered <see cref="IOpenIdConnectAuthService"/>: the application's flow bound to the application's resolver.
    /// </summary>
    /// <remarks>
    /// Scoped, so the resolver may be scoped; the flow it wraps is a singleton, since it caches discovery documents and
    /// keys.
    /// </remarks>
    internal sealed class OpenIdConnectAuthService : IOpenIdConnectAuthService
    {
        private readonly OpenIdConnectCodeFlow _flow;
        private readonly IOpenIdConnectProviderResolver _resolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectAuthService"/> class.
        /// </summary>
        /// <param name="flow">The application's flow, built at registration.</param>
        /// <param name="resolver">The application's resolver of the providers.</param>
        public OpenIdConnectAuthService(OpenIdConnectCodeFlow flow, IOpenIdConnectProviderResolver resolver)
        {
            _flow = flow;
            _resolver = resolver;
        }

        /// <inheritdoc />
        public Task<OpenIdConnectAuthBeginResult?> BeginAsync(OpenIdConnectAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            return _flow.BeginAsync(request, _resolver, httpContext, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<OpenIdConnectAuthCompletionResult> CompleteAsync(OpenIdConnectAuthorizationResponse response, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            return OpenIdConnectAuthCompletionResult.From(await _flow.CompleteAsync(response, _resolver, httpContext, cancellationToken));
        }
    }
}
