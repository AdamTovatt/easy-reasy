using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The registered <see cref="IOpenIdConnectAuthService"/>: the application's flow bound to the application's resolver.
    /// </summary>
    /// <remarks>
    /// A singleton, since the flow caches discovery documents and keys, while the resolver may be scoped (for example
    /// reading a database); so the resolver is taken from each request's services rather than injected.
    /// </remarks>
    internal sealed class OpenIdConnectAuthService : IOpenIdConnectAuthService
    {
        private readonly OpenIdConnectCodeFlow _flow;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectAuthService"/> class.
        /// </summary>
        /// <param name="flow">The application's flow, built at registration.</param>
        public OpenIdConnectAuthService(OpenIdConnectCodeFlow flow)
        {
            _flow = flow;
        }

        /// <inheritdoc />
        public async Task<OpenIdConnectAuthBeginResult> BeginAsync(OpenIdConnectAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(httpContext);

            OpenIdConnectProvider? provider = string.IsNullOrWhiteSpace(request.ProviderKey)
                ? null
                : await GetResolver(httpContext).FindProviderAsync(request.ProviderKey, cancellationToken);

            if (provider == null)
            {
                throw new ArgumentException($"The provider key '{request.ProviderKey}' resolves to no provider.", nameof(request));
            }

            return await _flow.BeginAsync(provider, request, httpContext, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<OpenIdConnectAuthCompletionResult> CompleteAsync(OpenIdConnectAuthorizationResponse response, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(httpContext);

            OpenIdConnectCodeFlowResult result = await _flow.CompleteAsync(response, GetResolver(httpContext), httpContext, cancellationToken);
            return OpenIdConnectAuthCompletionResult.From(result);
        }

        private static IOpenIdConnectProviderResolver GetResolver(HttpContext httpContext)
        {
            return httpContext.RequestServices.GetService<IOpenIdConnectProviderResolver>()
                ?? throw new InvalidOperationException(
                    $"{nameof(IOpenIdConnectProviderResolver)} is not registered in the DI container. Register your resolver, e.g.: " +
                    $"builder.Services.AddScoped<{nameof(IOpenIdConnectProviderResolver)}, MyProviderResolver>();");
        }
    }
}
