namespace EasyReasy.Auth
{
    /// <summary>
    /// The wire names of <see cref="OAuthClientAuthenticationMethod"/> (RFC 7591 section 2), the
    /// <c>token_endpoint_auth_method</c> values a registration and the server metadata use.
    /// </summary>
    public static class OAuthClientAuthenticationMethodNames
    {
        /// <summary><c>none</c>.</summary>
        public const string None = "none";

        /// <summary><c>client_secret_basic</c>.</summary>
        public const string ClientSecretBasic = "client_secret_basic";

        /// <summary><c>client_secret_post</c>.</summary>
        public const string ClientSecretPost = "client_secret_post";

        /// <summary>
        /// Every name, as the metadata lists them.
        /// </summary>
        public static IReadOnlyList<string> All { get; } = [None, ClientSecretBasic, ClientSecretPost];

        /// <summary>
        /// The wire name of a method.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>Its name.</returns>
        public static string ToName(OAuthClientAuthenticationMethod method)
        {
            return method switch
            {
                OAuthClientAuthenticationMethod.None => None,
                OAuthClientAuthenticationMethod.ClientSecretBasic => ClientSecretBasic,
                OAuthClientAuthenticationMethod.ClientSecretPost => ClientSecretPost,
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Not a defined client authentication method."),
            };
        }

        /// <summary>
        /// The method a wire name names.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="method">The method, when the name is known.</param>
        /// <returns>Whether the name is known.</returns>
        public static bool TryParse(string name, out OAuthClientAuthenticationMethod method)
        {
            switch (name)
            {
                case None:
                    method = OAuthClientAuthenticationMethod.None;
                    return true;
                case ClientSecretBasic:
                    method = OAuthClientAuthenticationMethod.ClientSecretBasic;
                    return true;
                case ClientSecretPost:
                    method = OAuthClientAuthenticationMethod.ClientSecretPost;
                    return true;
                default:
                    method = default;
                    return false;
            }
        }
    }
}
