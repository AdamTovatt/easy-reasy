using System.Text.Json;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The response of <c>POST /api/auth/saml/begin</c>: where to send the browser.
    /// </summary>
    public class SamlSignInBeginResponse
    {
        /// <summary>
        /// Gets the identity provider's single sign-on URL carrying the AuthnRequest. Send the browser here with a top-level
        /// navigation.
        /// </summary>
        public string RedirectUrl { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlSignInBeginResponse"/> class.
        /// </summary>
        /// <param name="redirectUrl">The identity provider's single sign-on URL carrying the AuthnRequest.</param>
        public SamlSignInBeginResponse(string redirectUrl)
        {
            RedirectUrl = redirectUrl;
        }

        /// <summary>
        /// Serializes this <see cref="SamlSignInBeginResponse"/> instance to a JSON string.
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
        /// Creates a <see cref="SamlSignInBeginResponse"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="SamlSignInBeginResponse"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="SamlSignInBeginResponse"/>.</exception>
        public static SamlSignInBeginResponse FromJson(string json)
        {
            try
            {
                SamlSignInBeginResponse? result = JsonSerializer.Deserialize<SamlSignInBeginResponse>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(SamlSignInBeginResponse)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(SamlSignInBeginResponse)} from the provided JSON.", jsonException);
            }
        }
    }
}
