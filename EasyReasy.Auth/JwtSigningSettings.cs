namespace EasyReasy.Auth
{
    /// <summary>
    /// The signing secret and issuer <see cref="AuthServiceCollectionExtensions.AddEasyReasyAuth(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, Action{EasyReasyAuthOptions}?)"/>
    /// registered, which the bearer validates every token against: the one place a token issuer is made from them. The
    /// first-party <see cref="IJwtTokenService"/> is made with the app's audience, and the OAuth server's connection token
    /// issuer with the resource as its audience, so both validate under the same scheme.
    /// </summary>
    internal sealed class JwtSigningSettings
    {
        private readonly string _secret;

        /// <summary>
        /// Initializes the settings.
        /// </summary>
        /// <param name="secret">The signing secret.</param>
        /// <param name="issuer">The issuer, or null.</param>
        public JwtSigningSettings(string secret, string? issuer)
        {
            _secret = secret;
            Issuer = issuer;
        }

        /// <summary>
        /// The issuer tokens carry, or null.
        /// </summary>
        public string? Issuer { get; }

        /// <summary>
        /// A token service that signs with this secret and issuer and sets <paramref name="audience"/>.
        /// </summary>
        /// <param name="audience">The audience the tokens carry, or null for none.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <returns>The token service.</returns>
        public JwtTokenService CreateTokenService(string? audience, TimeProvider timeProvider)
        {
            return new JwtTokenService(_secret, Issuer, audience, timeProvider);
        }
    }
}
