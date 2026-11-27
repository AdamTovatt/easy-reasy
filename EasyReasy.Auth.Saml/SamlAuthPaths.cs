namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// The paths <see cref="SamlAuthApplicationBuilderExtensions.AddSamlAuthEndpoints"/> maps.
    /// </summary>
    public static class SamlAuthPaths
    {
        /// <summary>
        /// The assertion consumer service, which takes the identity provider's form post.
        /// <see cref="SamlAuthOptions.AssertionConsumerServiceUrl"/> is this path as the browser reaches it.
        /// </summary>
        public const string AssertionConsumerService = "/api/auth/saml/acs";

        /// <summary>
        /// The sign-in begin endpoint, which the sign-in page posts the chosen identity provider to.
        /// </summary>
        public const string Begin = "/api/auth/saml/begin";

        /// <summary>
        /// The sign-in endpoint, which the completion page posts the completion code to.
        /// </summary>
        public const string SignIn = "/api/auth/saml";
    }
}
