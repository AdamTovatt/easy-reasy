namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// How the client secret is sent to the provider's token endpoint when the authorization code is redeemed.
    /// </summary>
    public enum OpenIdConnectClientAuthentication
    {
        /// <summary>
        /// <c>client_secret_basic</c>: the client id and secret in an HTTP Basic <c>Authorization</c> header, each
        /// form-encoded first as RFC 6749 requires. The method every provider must support.
        /// </summary>
        ClientSecretBasic,

        /// <summary>
        /// <c>client_secret_post</c>: the client id and secret as fields of the form body.
        /// </summary>
        ClientSecretPost,
    }
}
