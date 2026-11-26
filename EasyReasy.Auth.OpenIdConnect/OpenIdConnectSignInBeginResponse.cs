using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The response of a sign-in begin endpoint: where to send the browser.
    /// </summary>
    public class OpenIdConnectSignInBeginResponse
    {
        /// <summary>
        /// Gets the provider's authorize URL for the sign-in. Send the browser here with a top-level navigation.
        /// </summary>
        public string AuthorizationUrl { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectSignInBeginResponse"/> class.
        /// </summary>
        /// <param name="authorizationUrl">The provider's authorize URL for the sign-in.</param>
        public OpenIdConnectSignInBeginResponse(string authorizationUrl)
        {
            AuthorizationUrl = authorizationUrl;
        }

        /// <summary>
        /// Serializes this <see cref="OpenIdConnectSignInBeginResponse"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns the JSON representation of this instance.
        /// </summary>
        /// <returns>A JSON string representation of this instance.</returns>
        public override string ToString()
        {
            return ToJson();
        }

        /// <summary>
        /// Creates an <see cref="OpenIdConnectSignInBeginResponse"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>An <see cref="OpenIdConnectSignInBeginResponse"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into an <see cref="OpenIdConnectSignInBeginResponse"/>.</exception>
        public static OpenIdConnectSignInBeginResponse FromJson(string json)
        {
            try
            {
                OpenIdConnectSignInBeginResponse? result = JsonSerializer.Deserialize<OpenIdConnectSignInBeginResponse>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null || result.AuthorizationUrl == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(OpenIdConnectSignInBeginResponse)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(OpenIdConnectSignInBeginResponse)} from the provided JSON.", jsonException);
            }
        }
    }
}
