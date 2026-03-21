using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Request model for refreshing an access token using a refresh token.
    /// </summary>
    public class RefreshRequest
    {
        /// <summary>
        /// Gets the refresh token to use for obtaining a new access token.
        /// </summary>
        public string RefreshToken { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshRequest"/> class.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        public RefreshRequest(string refreshToken)
        {
            RefreshToken = refreshToken;
        }

        /// <summary>
        /// Serializes this <see cref="RefreshRequest"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="RefreshRequest"/> instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns a string representation of this <see cref="RefreshRequest"/> instance
        /// with the refresh token redacted to prevent accidental secret leakage in logs.
        /// </summary>
        /// <returns>A string representation with the refresh token replaced by "[REDACTED]".</returns>
        public override string ToString()
        {
            return $"{{\"refreshToken\":\"[REDACTED]\"}}";
        }

        /// <summary>
        /// Creates a <see cref="RefreshRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="RefreshRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="RefreshRequest"/>.</exception>
        public static RefreshRequest FromJson(string json)
        {
            try
            {
                RefreshRequest? result = JsonSerializer.Deserialize<RefreshRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(RefreshRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(RefreshRequest)} from the provided JSON.");
            }
        }
    }
}
