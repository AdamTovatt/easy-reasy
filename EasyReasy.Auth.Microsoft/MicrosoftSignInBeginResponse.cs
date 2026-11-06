using System.Text.Json;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The response of <c>POST /api/auth/microsoft/begin</c>: where to send the browser.
    /// </summary>
    public class MicrosoftSignInBeginResponse
    {
        /// <summary>
        /// Gets Microsoft's authorize URL for the sign-in. Send the browser here with a top-level navigation.
        /// </summary>
        public string AuthorizationUrl { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftSignInBeginResponse"/> class.
        /// </summary>
        /// <param name="authorizationUrl">Microsoft's authorize URL for the sign-in.</param>
        public MicrosoftSignInBeginResponse(string authorizationUrl)
        {
            AuthorizationUrl = authorizationUrl;
        }

        /// <summary>
        /// Serializes this <see cref="MicrosoftSignInBeginResponse"/> instance to a JSON string.
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
        /// Creates a <see cref="MicrosoftSignInBeginResponse"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="MicrosoftSignInBeginResponse"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="MicrosoftSignInBeginResponse"/>.</exception>
        public static MicrosoftSignInBeginResponse FromJson(string json)
        {
            try
            {
                MicrosoftSignInBeginResponse? result = JsonSerializer.Deserialize<MicrosoftSignInBeginResponse>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null || result.AuthorizationUrl == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(MicrosoftSignInBeginResponse)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(MicrosoftSignInBeginResponse)} from the provided JSON.", jsonException);
            }
        }
    }
}
