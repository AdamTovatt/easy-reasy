using System.Text.Json;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The body of <c>POST /api/auth/saml/begin</c>: the identity provider the person chose, and nothing else. Every
    /// security-relevant input of the sign-in comes from <see cref="ISamlAuthHandler.GetSignInSettingsAsync"/>.
    /// </summary>
    public class SamlSignInBeginRequest
    {
        /// <summary>
        /// Gets the key of the identity provider to sign in with.
        /// </summary>
        public string IdentityProviderKey { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SamlSignInBeginRequest"/> class.
        /// </summary>
        /// <param name="identityProviderKey">The key of the identity provider to sign in with.</param>
        public SamlSignInBeginRequest(string identityProviderKey)
        {
            IdentityProviderKey = identityProviderKey;
        }

        /// <summary>
        /// Serializes this <see cref="SamlSignInBeginRequest"/> instance to a JSON string.
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
        /// Creates a <see cref="SamlSignInBeginRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="SamlSignInBeginRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into a <see cref="SamlSignInBeginRequest"/>.</exception>
        public static SamlSignInBeginRequest FromJson(string json)
        {
            try
            {
                SamlSignInBeginRequest? result = JsonSerializer.Deserialize<SamlSignInBeginRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(SamlSignInBeginRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(SamlSignInBeginRequest)} from the provided JSON.", jsonException);
            }
        }
    }
}
