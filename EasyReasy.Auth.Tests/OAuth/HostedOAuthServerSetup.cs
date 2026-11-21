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
        /// How the pipeline runs authentication.
        /// </summary>
        public HostedOAuthServerPipeline Pipeline { get; init; } = HostedOAuthServerPipeline.UseEasyReasyAuth;
    }
}
