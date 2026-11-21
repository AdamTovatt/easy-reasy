using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The bearer scheme an OAuth connection's access token is validated by, and only that one. Its tokens are signed with
    /// a separate key <see cref="JwtSigningSettings"/> derives from the app's secret, and must carry
    /// <see cref="OAuthServerOptions.Resource"/> as their audience. The app's own bearer cannot validate one whatever its
    /// configuration, and this scheme runs only at endpoints opened with <see cref="AllowOAuthClientsAttribute"/>, so a
    /// connection reaches only what the app opened to it.
    /// </summary>
    /// <remarks>
    /// <c>AddEasyReasyAuth</c> registers it, so an endpoint opened without the OAuth server still runs: with no server
    /// there is no resource, and the scheme refuses every token. <c>AddEasyReasyOAuthServer</c> gives it the resource and
    /// both bearers the challenge naming the resource's metadata.
    /// </remarks>
    internal static class OAuthConnectionBearer
    {
        /// <summary>
        /// The scheme's name.
        /// </summary>
        public const string AuthenticationScheme = "EasyReasyOAuthConnection";

        /// <summary>
        /// The schemes an endpoint opened to connections authenticates with: the app's own bearer, so the app's tokens
        /// keep working there, and this one.
        /// </summary>
        public const string OpenedEndpointSchemes = JwtBearerDefaults.AuthenticationScheme + "," + AuthenticationScheme;

        /// <summary>
        /// Registers the scheme, for <c>AddEasyReasyAuth</c>. The resource is read from the OAuth server's settings when
        /// the options are built, so the server may be registered before or after.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public static void AddTo(IServiceCollection services)
        {
            services.AddAuthentication().AddJwtBearer(AuthenticationScheme);
            services.AddOptions<JwtBearerOptions>(AuthenticationScheme)
                .Configure<JwtSigningSettings, TimeProvider, IServiceProvider>((bearerOptions, signing, timeProvider, provider) =>
                {
                    string? resource = provider.GetService<OAuthServerSettings>()?.Resource;
                    bearerOptions.TokenValidationParameters = signing.CreateConnectionValidationParameters(resource, timeProvider);
                    bearerOptions.Events = new JwtBearerEvents { OnChallenge = ChallengeAfterTheAppsBearer };
                });
        }

        /// <summary>
        /// Gives both bearers the challenge naming the protected resource metadata (RFC 9728 section 5.1 and the MCP
        /// authorization spec), which is how an MCP client finds the server, for <c>AddEasyReasyOAuthServer</c>. A plain
        /// <c>Configure</c>, so a consumer's later <c>Configure</c> can change the app's.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="settings">The server's settings.</param>
        public static void ConfigureChallenges(IServiceCollection services, OAuthServerSettings settings)
        {
            string challenge = OAuthMetadataDocuments.BearerChallenge(settings);
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure(bearerOptions => bearerOptions.Challenge = challenge);
            services.AddOptions<JwtBearerOptions>(AuthenticationScheme)
                .Configure(bearerOptions => bearerOptions.Challenge = challenge);
        }

        // An opened endpoint challenges both schemes, the app's bearer first. When the app's bearer has answered, this one
        // adds nothing, unless the token was this scheme's own (its signature held, so it failed on its lifetime, audience
        // or issuer): then its error replaces the app bearer's, which could only say the signature was wrong.
        private static Task ChallengeAfterTheAppsBearer(JwtBearerChallengeContext context)
        {
            HttpResponse response = context.Response;
            if (response.HasStarted)
            {
                context.HandleResponse();
            }
            else if (response.Headers.WWWAuthenticate.Count > 0)
            {
                bool failedAsItsOwnToken = context.AuthenticateFailure != null
                    && context.AuthenticateFailure is not SecurityTokenInvalidSignatureException;
                if (failedAsItsOwnToken)
                {
                    response.Headers.Remove(HeaderNames.WWWAuthenticate);
                }
                else
                {
                    context.HandleResponse();
                }
            }

            return Task.CompletedTask;
        }
    }
}
