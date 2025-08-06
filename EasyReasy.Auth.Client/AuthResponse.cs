using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Client
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
        /// Initializes a new instance of the <see cref="AuthResponse"/> class.
        /// </summary>
        /// <param name="token">The JWT token for authentication.</param>
        /// <param name="expiresAt">The expiration date/time of the token in ISO 8601 format (UTC).</param>
        public AuthResponse(string token, string expiresAt)
        {
            Token = token;
            ExpiresAt = expiresAt;
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
        /// Returns a JSON string representation of this <see cref="AuthResponse"/> instance.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="AuthResponse"/> instance.</returns>
        public override string ToString()
        {
            return ToJson();
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
                    throw new ArgumentException($"Failed to deserialize {nameof(AuthResponse)} from json: {json}");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(AuthResponse)} from json: {json}", jsonException);
            }
        }
    }
}