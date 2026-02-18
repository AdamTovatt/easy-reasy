using System.Text.Json;
using EasyReasy.Auth;

namespace EasyReasy.Auth.Google
{
    /// <summary>
    /// Request model for Google authentication containing the Google ID token.
    /// </summary>
    public class GoogleAuthRequest
    {
        /// <summary>
        /// Gets the Google ID token to validate.
        /// </summary>
        public string IdToken { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleAuthRequest"/> class.
        /// </summary>
        /// <param name="idToken">The Google ID token to validate.</param>
        public GoogleAuthRequest(string idToken)
        {
            IdToken = idToken;
        }

        /// <summary>
        /// Serializes this <see cref="GoogleAuthRequest"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="GoogleAuthRequest"/> instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns a JSON string representation of this <see cref="GoogleAuthRequest"/> instance.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="GoogleAuthRequest"/> instance.</returns>
        public override string ToString()
        {
            return ToJson();
        }

        /// <summary>
        /// Creates a <see cref="GoogleAuthRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="GoogleAuthRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="GoogleAuthRequest"/>.</exception>
        public static GoogleAuthRequest FromJson(string json)
        {
            try
            {
                GoogleAuthRequest? result = JsonSerializer.Deserialize<GoogleAuthRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(GoogleAuthRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(GoogleAuthRequest)} from the provided JSON.", jsonException);
            }
        }
    }
}
