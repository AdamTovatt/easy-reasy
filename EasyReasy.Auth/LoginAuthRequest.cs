using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Request model for username/password authentication.
    /// </summary>
    public class LoginAuthRequest
    {
        /// <summary>
        /// Gets the username for authentication.
        /// </summary>
        public string Username { get; }

        /// <summary>
        /// Gets the password for authentication.
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoginAuthRequest"/> class.
        /// </summary>
        /// <param name="username">The username for authentication.</param>
        /// <param name="password">The password for authentication.</param>
        public LoginAuthRequest(string username, string password)
        {
            Username = username;
            Password = password;
        }

        /// <summary>
        /// Serializes this <see cref="LoginAuthRequest"/> instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="LoginAuthRequest"/> instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Returns a JSON string representation of this <see cref="LoginAuthRequest"/> instance.
        /// </summary>
        /// <returns>A JSON string representation of this <see cref="LoginAuthRequest"/> instance.</returns>
        public override string ToString()
        {
            return ToJson();
        }

        /// <summary>
        /// Creates a <see cref="LoginAuthRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="LoginAuthRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="LoginAuthRequest"/>.</exception>
        public static LoginAuthRequest FromJson(string json)
        {
            try
            {
                LoginAuthRequest? result = JsonSerializer.Deserialize<LoginAuthRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(LoginAuthRequest)} from json: {json}");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(LoginAuthRequest)} from json: {json}", jsonException);
            }
        }
    }
}