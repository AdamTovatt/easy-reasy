using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The token endpoint's successful response (RFC 6749 section 5.1).
    /// </summary>
    internal sealed class OAuthTokenResponse
    {
        /// <summary>
        /// The access token.
        /// </summary>
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        /// <summary>
        /// The token type, always <c>Bearer</c>.
        /// </summary>
        [JsonPropertyName("token_type")]
        public string TokenType => "Bearer";

        /// <summary>
        /// The access token's lifetime in seconds.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public required long ExpiresIn { get; init; }

        /// <summary>
        /// The refresh token that replaces the one presented, or the first one for a redeemed code.
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public required string RefreshToken { get; init; }
    }
}
