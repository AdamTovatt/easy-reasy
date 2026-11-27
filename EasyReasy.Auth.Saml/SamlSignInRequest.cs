using System.Text.Json;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The body of <c>POST /api/auth/saml</c>: the completion code the assertion consumer service redirected the browser to
    /// the completion page with.
    /// </summary>
    public class SamlSignInRequest
    {
        /// <summary>
        /// Gets the completion code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlSignInRequest"/> class.
        /// </summary>
        /// <param name="code">The completion code from the redirect's <c>code</c> query parameter.</param>
        public SamlSignInRequest(string code)
        {
            Code = code;
        }

        /// <summary>
        /// Serializes this <see cref="SamlSignInRequest"/> instance to a JSON string.
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
        /// Creates a <see cref="SamlSignInRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="SamlSignInRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="SamlSignInRequest"/>.</exception>
        public static SamlSignInRequest FromJson(string json)
        {
            try
            {
                SamlSignInRequest? result = JsonSerializer.Deserialize<SamlSignInRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(SamlSignInRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(SamlSignInRequest)} from the provided JSON.", jsonException);
            }
        }
    }
}
