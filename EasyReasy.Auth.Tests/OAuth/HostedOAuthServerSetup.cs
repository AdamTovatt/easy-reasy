using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// How <see cref="HostedOAuthServer"/> registers EasyReasy.Auth and builds its pipeline, for the tests of an app set up
    /// another way than the default.
    /// </summary>
    internal sealed record HostedOAuthServerSetup
    {
        /// <summary>
        /// The app's <see cref="EasyReasyAuthOptions.Audience"/>, or null for none, which turns the app's own audience
        /// check off.
        /// </summary>
        public string? Audience { get; init; } = HostedOAuthServer.Audience;

        /// <summary>
        /// <see cref="EasyReasyAuthOptions.RegisterJwtTokenService"/>.
        /// </summary>
        public bool RegisterJwtTokenService { get; init; } = true;

        /// <summary>
        /// Whether <c>AddEasyReasyOAuthServer</c> is called before <c>AddEasyReasyAuth</c> rather than after.
        /// </summary>
        public bool OAuthServerRegisteredFirst { get; init; }

        /// <summary>
        /// Whether the app's bearer answers a challenge and a forbid with a JSON body of its own, and no
        /// <c>WWW-Authenticate</c> header, so the response has started before the connection's scheme is asked.
        /// </summary>
        public bool AppBearerWritesItsOwnBody { get; init; }

        /// <summary>
        /// A logger provider the host logs to, or null for none.
        /// </summary>
        public ILoggerProvider? LoggerProvider { get; init; }

        /// <summary>
        /// How the pipeline runs authentication.
        /// </summary>
        public HostedOAuthServerPipeline Pipeline { get; init; } = HostedOAuthServerPipeline.UseEasyReasyAuth;
    }
}
