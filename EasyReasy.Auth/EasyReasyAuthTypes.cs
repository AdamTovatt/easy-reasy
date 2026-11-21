namespace EasyReasy.Auth
{
    /// <summary>
    /// The authentication types the library's policies know, as the <see cref="EasyReasyClaimTypes.AuthType"/> claim
    /// carries them. Pass these to <see cref="IJwtTokenService.CreateToken"/> and the session methods rather than
    /// spelling the value again.
    /// </summary>
    public static class EasyReasyAuthTypes
    {
        /// <summary>
        /// A person signed in to the application (<c>"user"</c>): what the <c>UserOnly</c> policy requires, and the only
        /// auth type that may approve or deny an OAuth authorization request.
        /// </summary>
        public const string User = "user";

        /// <summary>
        /// An API key (<c>"apikey"</c>): what the <c>ApiKeyOnly</c> policy requires.
        /// </summary>
        public const string ApiKey = "apikey";
    }
}
