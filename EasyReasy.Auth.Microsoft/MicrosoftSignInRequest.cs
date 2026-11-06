using System.Text.Json;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The body of <c>POST /api/auth/microsoft</c>: the <c>code</c> and <c>state</c> Microsoft's redirect brought back
    /// to the sign-in page.
    /// </summary>
    public class MicrosoftSignInRequest
    {
        /// <summary>
        /// Gets the authorization code from the redirect.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the <c>state</c> from the redirect.
        /// </summary>
        public string State { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftSignInRequest"/> class.
        /// </summary>
        /// <param name="code">The authorization code from the redirect.</param>
        /// <param name="state">The <c>state</c> from the redirect.</param>
        public MicrosoftSignInRequest(string code, string state)
        {
            Code = code;
            State = state;
        }

        /// <summary>
        /// Serializes this <see cref="MicrosoftSignInRequest"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns a string representation of this instance with the code and state redacted, so neither leaks into
        /// a log: the code can be redeemed, and the state carries the flow's PKCE verifier, encrypted.
        /// </summary>
        /// <returns>A string representation with both values replaced by "[REDACTED]".</returns>
        public override string ToString()
        {
            return "{\"code\":\"[REDACTED]\",\"state\":\"[REDACTED]\"}";
        }

        /// <summary>
        /// Creates a <see cref="MicrosoftSignInRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="MicrosoftSignInRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="MicrosoftSignInRequest"/>.</exception>
        public static MicrosoftSignInRequest FromJson(string json)
        {
            try
            {
                MicrosoftSignInRequest? result = JsonSerializer.Deserialize<MicrosoftSignInRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(MicrosoftSignInRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(MicrosoftSignInRequest)} from the provided JSON.", jsonException);
            }
        }
    }
}
