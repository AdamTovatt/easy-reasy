using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Response model for successful JWT token authentication.
    /// </summary>
    public class AuthResponse
    {
        /// <summary>
        /// The JWT token for authentication.
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; }

        /// <summary>
        /// The expiration date/time of the token in ISO 8601 format (UTC).
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public string ExpiresAt { get; set; }

        /// <summary>
        /// The refresh token for obtaining a new access token. Null if refresh tokens are not enabled.
        /// </summary>
        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthResponse"/> class.
        /// </summary>
        /// <param name="token">The JWT token for authentication.</param>
        /// <param name="expiresAt">The expiration date/time of the token in ISO 8601 format (UTC).</param>
        /// <param name="refreshToken">The refresh token for obtaining a new access token, or null if refresh tokens are not enabled.</param>
        public AuthResponse(string token, string expiresAt, string? refreshToken = null)
        {
            Token = token;
            ExpiresAt = expiresAt;
            RefreshToken = refreshToken;
        }

        /// <summary>
        /// Serializes this <see cref="AuthResponse"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="AuthResponse"/> instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Returns a string representation of this <see cref="AuthResponse"/> instance
        /// with the token and refresh token redacted to prevent accidental secret leakage in logs.
        /// </summary>
        /// <returns>A string representation with sensitive fields replaced by "[REDACTED]".</returns>
        public override string ToString()
        {
            string escapedExpiresAt = JsonSerializer.Serialize(ExpiresAt);
            string refreshTokenPart = RefreshToken != null ? ",\"refreshToken\":\"[REDACTED]\"" : "";
            return $"{{\"token\":\"[REDACTED]\",\"expiresAt\":{escapedExpiresAt}{refreshTokenPart}}}";
        }

        /// <summary>
        /// Creates an <see cref="AuthResponse"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>An <see cref="AuthResponse"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into an <see cref="AuthResponse"/>.</exception>
        public static AuthResponse FromJson(string json)
        {
            try
            {
                AuthResponse? result = JsonSerializer.Deserialize<AuthResponse>(json);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(AuthResponse)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(AuthResponse)} from the provided JSON.");
            }
        }
    }
}