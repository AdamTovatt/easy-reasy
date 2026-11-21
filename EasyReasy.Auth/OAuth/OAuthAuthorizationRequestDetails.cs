using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// What a pending authorization request asks, as <c>GET /oauth/authorize/requests/{request}</c> returns it for the
    /// consent page to show: who is asking, where the code will go, and what was requested.
    /// </summary>
    public sealed class OAuthAuthorizationRequestDetails
    {
        /// <summary>
        /// The client asking to connect. Show its name as the client's own claim beside the facts: the metadata document
        /// host when there is one, and the redirect URI's host.
        /// </summary>
        [JsonPropertyName("client")]
        public required OAuthClientInfo Client { get; init; }

        /// <summary>
        /// Where the code will be sent. The MCP authorization spec asks the consent page to show its host clearly.
        /// </summary>
        [JsonPropertyName("redirectUri")]
        public required string RedirectUri { get; init; }

        /// <summary>
        /// The requested scopes, empty when none were requested.
        /// </summary>
        [JsonPropertyName("scopes")]
        public required IReadOnlyList<string> Scopes { get; init; }

        /// <summary>
        /// The resource the client named, or null.
        /// </summary>
        [JsonPropertyName("resource")]
        public string? Resource { get; init; }

        /// <summary>
        /// When the request can no longer be approved or denied, ISO 8601 UTC.
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public required string ExpiresAt { get; init; }

        /// <summary>
        /// Serializes these details to JSON, as the endpoint writes them.
        /// </summary>
        /// <returns>The JSON.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, OAuthJson.Options);
        }

        /// <summary>
        /// Reads details from the JSON the endpoint writes.
        /// </summary>
        /// <param name="json">The JSON.</param>
        /// <returns>The details.</returns>
        /// <exception cref="ArgumentException">The JSON is not such details.</exception>
        public static OAuthAuthorizationRequestDetails FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<OAuthAuthorizationRequestDetails>(json, OAuthJson.Options)
                    ?? throw new ArgumentException($"Failed to deserialize {nameof(OAuthAuthorizationRequestDetails)} from the provided JSON.");
            }
            catch (JsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(OAuthAuthorizationRequestDetails)} from the provided JSON.");
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToJson();
        }
    }
}
