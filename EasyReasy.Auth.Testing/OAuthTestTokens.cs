namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// A token response from the authorization server's token endpoint.
    /// </summary>
    public sealed class OAuthTestTokens
    {
        /// <summary>
        /// The access token, to send as <c>Authorization: Bearer</c>.
        /// </summary>
        public required string AccessToken { get; init; }

        /// <summary>
        /// The token type, <c>Bearer</c>.
        /// </summary>
        public required string TokenType { get; init; }

        /// <summary>
        /// The access token's lifetime in seconds.
        /// </summary>
        public required long ExpiresIn { get; init; }

        /// <summary>
        /// The refresh token, single use: refreshing returns the next one.
        /// </summary>
        public required string RefreshToken { get; init; }

        /// <summary>
        /// Returns a redacted form, since both tokens are credentials.
        /// </summary>
        /// <returns>The redacted form.</returns>
        public override string ToString()
        {
            return $"{{\"token_type\":\"{TokenType}\",\"expires_in\":{ExpiresIn},\"access_token\":\"[REDACTED]\",\"refresh_token\":\"[REDACTED]\"}}";
        }
    }
}
