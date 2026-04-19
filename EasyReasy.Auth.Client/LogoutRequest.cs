using System.Text.Json;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Request model for logging out by revoking a refresh token family.
    /// </summary>
    public sealed class LogoutRequest
    {
        /// <summary>
        /// Gets the refresh token whose family should be invalidated.
        /// </summary>
        public string RefreshToken { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogoutRequest"/> class.
        /// </summary>
        /// <param name="refreshToken">The refresh token to log out.</param>
        public LogoutRequest(string refreshToken)
        {
            RefreshToken = refreshToken;
        }

        /// <summary>
        /// Serializes this <see cref="LogoutRequest"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="LogoutRequest"/> instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns a string representation of this <see cref="LogoutRequest"/> instance
        /// with the refresh token redacted to prevent accidental secret leakage in logs.
        /// </summary>
        /// <returns>A string representation with the refresh token replaced by "[REDACTED]".</returns>
        public override string ToString()
        {
            return $"{{\"refreshToken\":\"[REDACTED]\"}}";
        }

        /// <summary>
        /// Creates a <see cref="LogoutRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="LogoutRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="LogoutRequest"/>.</exception>
        public static LogoutRequest FromJson(string json)
        {
            try
            {
                LogoutRequest? result = JsonSerializer.Deserialize<LogoutRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(LogoutRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(LogoutRequest)} from the provided JSON.");
            }
        }
    }
}
