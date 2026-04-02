using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Client
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
        /// Gets the optional client identifier associated with this API key.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ClientId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiKeyAuthRequest"/> class.
        /// </summary>
        /// <param name="apiKey">The API key for authentication.</param>
        /// <param name="clientId">The optional client identifier associated with this API key.</param>
        public ApiKeyAuthRequest(string apiKey, string? clientId = null)
        {
            ApiKey = apiKey;
            ClientId = clientId;
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
        /// Returns a string representation of this <see cref="ApiKeyAuthRequest"/> instance
        /// with the API key redacted to prevent accidental secret leakage in logs.
        /// </summary>
        /// <returns>A string representation with the API key replaced by "[REDACTED]".</returns>
        public override string ToString()
        {
            string clientIdPart = ClientId != null ? $",\"clientId\":{JsonSerializer.Serialize(ClientId)}" : "";
            return $"{{\"apiKey\":\"[REDACTED]\"{clientIdPart}}}";
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
                    throw new ArgumentException($"Failed to deserialize {nameof(ApiKeyAuthRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(ApiKeyAuthRequest)} from the provided JSON.");
            }
        }
    }
}