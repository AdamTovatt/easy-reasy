using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Request model for API key authentication.
    /// </summary>
    public class ApiKeyAuthRequest
    {
        /// <summary>
        /// Gets the API key for authentication.
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiKeyAuthRequest"/> class.
        /// </summary>
        /// <param name="apiKey">The API key for authentication.</param>
        public ApiKeyAuthRequest(string apiKey)
        {
            ApiKey = apiKey;
        }

        /// <summary>
        /// Serializes this <see cref="ApiKeyAuthRequest"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="ApiKeyAuthRequest"/> instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns a JSON string representation of this <see cref="ApiKeyAuthRequest"/> instance.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="ApiKeyAuthRequest"/> instance.</returns>
        public override string ToString()
        {
            return ToJson();
        }

        /// <summary>
        /// Creates an <see cref="ApiKeyAuthRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>An <see cref="ApiKeyAuthRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into an <see cref="ApiKeyAuthRequest"/>.</exception>
        public static ApiKeyAuthRequest FromJson(string json)
        {
            try
            {
                ApiKeyAuthRequest? result = JsonSerializer.Deserialize<ApiKeyAuthRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(ApiKeyAuthRequest)} from json: {json}");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(ApiKeyAuthRequest)} from json: {json}", jsonException);
            }
        }
    }
}