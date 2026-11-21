using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Where the consent page sends the browser once the person has decided, as the approve and deny endpoints return it:
    /// the client's redirect URI with the code (or <c>error=access_denied</c>), the client's <c>state</c> and the issuer
    /// (<c>iss</c>, RFC 9207).
    /// </summary>
    public sealed class OAuthConsentRedirect
    {
        /// <summary>
        /// The URL to navigate the browser to.
        /// </summary>
        [JsonPropertyName("redirectUri")]
        public required string RedirectUri { get; init; }

        /// <summary>
        /// Serializes the redirect to JSON, as the endpoints write it.
        /// </summary>
        /// <returns>The JSON.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, OAuthJson.Options);
        }

        /// <summary>
        /// Reads a redirect from the JSON the endpoints write.
        /// </summary>
        /// <param name="json">The JSON.</param>
        /// <returns>The redirect.</returns>
        /// <exception cref="ArgumentException">The JSON is not such a redirect.</exception>
        public static OAuthConsentRedirect FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<OAuthConsentRedirect>(json, OAuthJson.Options)
                    ?? throw new ArgumentException($"Failed to deserialize {nameof(OAuthConsentRedirect)} from the provided JSON.");
            }
            catch (JsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(OAuthConsentRedirect)} from the provided JSON.");
            }
        }

        /// <summary>
        /// Returns a redacted form: the redirect carries an authorization code, a credential until it is redeemed.
        /// </summary>
        /// <returns>The redacted JSON.</returns>
        public override string ToString()
        {
            return "{\"redirectUri\":\"[REDACTED]\"}";
        }
    }
}
