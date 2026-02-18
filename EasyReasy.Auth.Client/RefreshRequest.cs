using System.Text.Json;

namespace EasyReasy.Auth.Client
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
        /// Returns a JSON string representation of this <see cref="RefreshRequest"/> instance.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="RefreshRequest"/> instance.</returns>
        public override string ToString()
        {
            return ToJson();
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
                    throw new ArgumentException($"Failed to deserialize {nameof(RefreshRequest)} from json: {json}");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(RefreshRequest)} from json: {json}", jsonException);
            }
        }
    }
}
