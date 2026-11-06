using System.Text.Json;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The optional body of <c>POST /api/auth/microsoft/begin</c>. It carries a login hint and nothing else: every
    /// security-relevant input of the sign-in comes from <see cref="IMicrosoftAuthHandler.GetSignInSettingsAsync"/>.
    /// </summary>
    public class MicrosoftSignInBeginRequest
    {
        /// <summary>
        /// Gets the account's sign-in name, sent to Microsoft as <c>login_hint</c>, or null.
        /// </summary>
        public string? LoginHint { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftSignInBeginRequest"/> class.
        /// </summary>
        /// <param name="loginHint">The account's sign-in name, or null.</param>
        public MicrosoftSignInBeginRequest(string? loginHint)
        {
            LoginHint = loginHint;
        }

        /// <summary>
        /// Serializes this <see cref="MicrosoftSignInBeginRequest"/> instance to a JSON string.
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
        /// Creates a <see cref="MicrosoftSignInBeginRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="MicrosoftSignInBeginRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="MicrosoftSignInBeginRequest"/>.</exception>
        public static MicrosoftSignInBeginRequest FromJson(string json)
        {
            try
            {
                MicrosoftSignInBeginRequest? result = JsonSerializer.Deserialize<MicrosoftSignInBeginRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(MicrosoftSignInBeginRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(MicrosoftSignInBeginRequest)} from the provided JSON.", jsonException);
            }
        }
    }
}
