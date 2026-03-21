namespace EasyReasy.Auth
{
    /// <summary>
    /// Configuration options for EasyReasy JWT authentication and authorization.
    /// </summary>
    public class EasyReasyAuthOptions
    {
        /// <summary>
        /// The expected issuer for JWT tokens. If null (default), issuer validation is disabled.
        /// </summary>
        public string? Issuer { get; set; }

        /// <summary>
        /// The expected audience for JWT tokens. If null (default), audience validation is disabled.
        /// When set, tokens must contain a matching <c>aud</c> claim to be accepted.
        /// This prevents tokens issued for one service from being accepted by another.
        /// </summary>
        public string? Audience { get; set; }

        /// <summary>
        /// The clock skew tolerance for token lifetime validation. Default is 30 seconds.
        /// The <c>Microsoft.IdentityModel</c> default is 5 minutes, which is often too generous.
        /// Increase this if you see tokens rejected due to clock drift between servers.
        /// </summary>
        public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether to automatically register <see cref="IJwtTokenService"/> in the DI container.
        /// Default is <c>true</c>. Set to <c>false</c> if you want to register your own implementation.
        /// </summary>
        public bool RegisterJwtTokenService { get; set; } = true;

        /// <summary>
        /// Validates the options and throws if any values are invalid.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="ClockSkew"/> is negative.</exception>
        internal void Validate()
        {
            if (ClockSkew < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ClockSkew), "Must be non-negative.");
            }
        }
    }
}
