using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Extension methods for registering the OAuth 2.1 authorization server.
    /// </summary>
    public static class OAuthServerServiceCollectionExtensions
    {
        /// <summary>
        /// The name of the <see cref="HttpClient"/> the server fetches client ID metadata documents with. Its primary
        /// handler follows no redirects, sends no cookies, uses no proxy and connects only to public addresses. The
        /// address check runs on the connection itself, so it holds only without a proxy (through one, it would check the
        /// proxy's address): replacing the primary handler, or giving it a proxy, removes that guard.
        /// </summary>
        public const string MetadataDocumentHttpClientName = "EasyReasyOAuthMetadataDocuments";

        /// <summary>
        /// How long a metadata document fetch may take, connection included.
        /// </summary>
        private static readonly TimeSpan MetadataDocumentFetchTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Registers the OAuth 2.1 authorization server: the application's store and grant handler (both scoped), the
        /// server's own services, <see cref="IOAuthGrantService"/>, the hosted sweep that deletes expired rows through
        /// <see cref="IOAuthServerStore.DeleteExpiredAsync"/> every <see cref="OAuthServerOptions.SweepInterval"/>, and
        /// the JWT bearer's 401 challenge pointing clients at the protected resource metadata. Map the endpoints with
        /// <see cref="OAuthServerApplicationBuilderExtensions.AddOAuthServerEndpoints"/>.
        /// </summary>
        /// <remarks>
        /// Call it after <see cref="AuthServiceCollectionExtensions.AddEasyReasyAuth(IServiceCollection, string, Action{EasyReasyAuthOptions}?)"/>:
        /// the tokens are JWTs with that registration's issuer, signed with a separate key derived from its secret, with
        /// <see cref="OAuthServerOptions.Resource"/> as their audience. They are validated by a bearer scheme of their own,
        /// which only endpoints opened with
        /// <see cref="OAuthClientEndpointConventionBuilderExtensions.AllowOAuthClients{TBuilder}(TBuilder)"/> or
        /// <see cref="AllowOAuthClientsAttribute"/> run; the app's own bearer cannot validate one. Registers
        /// <see cref="TimeProvider.System"/> as the clock unless one is registered already.
        /// </remarks>
        /// <typeparam name="TStore">The application's <see cref="IOAuthServerStore"/>.</typeparam>
        /// <typeparam name="THandler">The application's <see cref="IOAuthGrantHandler"/>.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="options">The server's options.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">The options are invalid; every problem is reported in one exception.</exception>
        public static IServiceCollection AddEasyReasyOAuthServer<TStore, THandler>(this IServiceCollection services, OAuthServerOptions options)
            where TStore : class, IOAuthServerStore
            where THandler : class, IOAuthGrantHandler
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);
            OAuthServerSettings settings = OAuthServerSettings.Create(options);

            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton(settings);
            services.AddScoped<IOAuthServerStore, TStore>();
            services.AddScoped<IOAuthGrantHandler, THandler>();
            services.AddScoped<IOAuthGrantService, OAuthGrantService>();
            services.AddHostedService<OAuthServerSweep>();

            services.AddHttpClient(MetadataDocumentHttpClientName, client => client.Timeout = MetadataDocumentFetchTimeout)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    UseProxy = false,
                    ConnectTimeout = MetadataDocumentFetchTimeout,
                    ConnectCallback = PublicNetworkAddress.ConnectAsync,
                });
            services.AddSingleton<OAuthClientMetadataDocumentFetcher>();

            services.AddScoped<OAuthClientDirectory>();
            services.AddScoped<OAuthClientAuthenticator>();
            services.AddScoped(provider => new OAuthRegistrationEndpoint(
                provider.GetRequiredService<IOAuthServerStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<IAuthAuditLogger>()));
            services.AddScoped(provider => new OAuthAuthorizationEndpoint(
                provider.GetRequiredService<OAuthServerSettings>(),
                provider.GetRequiredService<IOAuthServerStore>(),
                provider.GetRequiredService<OAuthClientDirectory>(),
                provider.GetRequiredService<IOAuthGrantHandler>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<IAuthAuditLogger>()));
            services.AddScoped(provider => new OAuthTokenEndpoint(
                provider.GetRequiredService<OAuthServerSettings>(),
                provider.GetRequiredService<IOAuthServerStore>(),
                provider.GetRequiredService<OAuthClientAuthenticator>(),
                provider.GetRequiredService<IOAuthGrantHandler>(),
                provider.GetRequiredService<JwtSigningSettings>().CreateConnectionTokenService(settings.Resource, provider.GetRequiredService<TimeProvider>()),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<IAuthAuditLogger>()));
            services.AddScoped(provider => new OAuthRevocationEndpoint(
                provider.GetRequiredService<IOAuthServerStore>(),
                provider.GetRequiredService<OAuthClientAuthenticator>(),
                provider.GetService<IAuthAuditLogger>()));

            // A connection's access token is issued for the resource (RFC 8707, as the MCP spec requires) and validated only
            // by its own scheme, which AddEasyReasyAuth registers, which reads the resource from these settings, and which
            // endpoints opened with AllowOAuthClients run. A 401 from either bearer names the resource's metadata.
            OAuthConnectionBearer.ConfigureChallenges(services, settings);

            return services;
        }
    }
}
