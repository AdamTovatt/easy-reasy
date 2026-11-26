using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The body of <c>POST /api/auth/oidc/begin</c>: the provider the person chose and an optional login hint, and nothing
    /// else. Every security-relevant input of the sign-in comes from
    /// <see cref="IOpenIdConnectAuthHandler.GetSignInSettingsAsync"/>.
    /// </summary>
    public class OpenIdConnectSignInBeginRequest
    {
        /// <summary>
        /// Gets the key of the provider to sign in with.
        /// </summary>
        public string ProviderKey { get; }

        /// <summary>
        /// Gets the account's sign-in name, sent to the provider as <c>login_hint</c>, or null.
        /// </summary>
        public string? LoginHint { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdConnectSignInBeginRequest"/> class.
        /// </summary>
        /// <param name="providerKey">The key of the provider to sign in with.</param>
        /// <param name="loginHint">The account's sign-in name, or null.</param>
        public OpenIdConnectSignInBeginRequest(string providerKey, string? loginHint)
        {
            ProviderKey = providerKey;
            LoginHint = loginHint;
        }

        /// <summary>
        /// Serializes this <see cref="OpenIdConnectSignInBeginRequest"/> instance to a JSON string.
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
        /// Creates an <see cref="OpenIdConnectSignInBeginRequest"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>An <see cref="OpenIdConnectSignInBeginRequest"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the JSON cannot be deserialized into an <see cref="OpenIdConnectSignInBeginRequest"/>.</exception>
        public static OpenIdConnectSignInBeginRequest FromJson(string json)
        {
            try
            {
                OpenIdConnectSignInBeginRequest? result = JsonSerializer.Deserialize<OpenIdConnectSignInBeginRequest>(json, JsonSerializerSettings.CurrentOptions);

                if (result == null)
                {
                    throw new ArgumentException($"Failed to deserialize {nameof(OpenIdConnectSignInBeginRequest)} from the provided JSON.");
                }

                return result;
            }
            catch (JsonException jsonException)
            {
                throw new ArgumentException($"Failed to deserialize {nameof(OpenIdConnectSignInBeginRequest)} from the provided JSON.", jsonException);
            }
        }
    }
}
