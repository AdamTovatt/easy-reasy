using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// What the provider's redirect brought back to the redirect URI page, as that page posts it: the <c>code</c>, the
    /// <c>state</c>, the <c>iss</c> parameter, and the redirect URI the page itself is at. The input to a flow's completion
    /// and the body of the sign-in endpoint.
    /// </summary>
    /// <remarks>
    /// The page sends its own address because that is where the provider sent the browser: completion refuses a response
    /// that arrived at another redirect URI than the flow named. With a redirect URI of its own per provider, that is
    /// what stops one provider from passing off another provider's response as its own (a mix-up).
    /// </remarks>
    public class OpenIdConnectAuthorizationResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectAuthorizationResponse"/> class.
        /// </summary>
        /// <param name="code">The authorization code from the redirect.</param>
        /// <param name="state">The <c>state</c> from the redirect.</param>
        /// <param name="issuer">The <c>iss</c> parameter from the redirect, or null when it had none.</param>
        /// <param name="redirectUri">The redirect URI the response arrived at: the page's address without its query.</param>
        public OpenIdConnectAuthorizationResponse(string code, string state, string? issuer, string redirectUri)
        {
            Code = code;
            State = state;
            Issuer = issuer;
            RedirectUri = redirectUri;
        }

        /// <summary>
        /// Gets the authorization code from the redirect.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the <c>state</c> from the redirect.
        /// </summary>
        public string State { get; }

        /// <summary>
        /// Gets the <c>iss</c> parameter from the redirect (RFC 9207), or null when it had none. Compared with the
        /// provider's issuer when present, and required when the provider's discovery document says it sends one.
        /// </summary>
        [JsonPropertyName("iss")]
        public string? Issuer { get; }

        /// <summary>
        /// Gets the redirect URI the response arrived at: the page's address without its query, for example
        /// <c>https://app.example/auth/okta/callback</c>.
        /// </summary>
        public string RedirectUri { get; }

        /// <summary>
        /// Serializes this <see cref="OpenIdConnectAuthorizationResponse"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns a string representation of this instance with the code and state redacted, so neither leaks into a
        /// log: the code can be redeemed, and the state carries the flow's PKCE verifier, encrypted.
        /// </summary>
        /// <returns>A string representation with the code and state replaced by "[REDACTED]".</returns>
        public override string ToString()
        {
            return JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["code"] = "[REDACTED]",
                ["state"] = "[REDACTED]",
                ["iss"] = Issuer,
                ["redirectUri"] = RedirectUri,
            });
        }

        /// <summary>
        /// Creates an <see cref="OpenIdConnectAuthorizationResponse"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>An <see cref="OpenIdConnectAuthorizationResponse"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into an <see cref="OpenIdConnectAuthorizationResponse"/>.</exception>
        public static OpenIdConnectAuthorizationResponse FromJson(string json)
        {
            try
            {
                OpenIdConnectAuthorizationResponse? result = JsonSerializer.Deserialize<OpenIdConnectAuthorizationResponse>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(OpenIdConnectAuthorizationResponse)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(OpenIdConnectAuthorizationResponse)} from the provided JSON.", jsonException);
            }
        }
    }
}
