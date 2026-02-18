namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Configuration options for Google authentication.
    /// </summary>
    public class GoogleAuthOptions
    {
        /// <summary>
        /// The Google OAuth 2.0 client ID used to validate ID tokens.
        /// </summary>
        public required string ClientId { get; init; }

        /// <summary>
        /// An optional collection of allowed Google Workspace hosted domains.
        /// When set, only users from these domains will be accepted. Matching is case-insensitive.
        /// </summary>
        public ICollection<string>? AllowedHostedDomains { get; init; }

        /// <summary>
        /// Whether to reject Google accounts whose email address is not verified (the token's
        /// <c>email_verified</c> claim is false). Defaults to <c>true</c>, matching Google's guidance
        /// that an unverified email must not be trusted as an identifier. Set to <c>false</c> only if
        /// your application does not rely on the email address for identity.
        /// </summary>
        public bool RequireVerifiedEmail { get; init; } = true;
    }
}
