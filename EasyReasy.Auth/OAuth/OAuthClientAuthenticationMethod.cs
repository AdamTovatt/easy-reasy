namespace EasyReasy.Auth
{
    /// <summary>
    /// How a client authenticates at the token and revocation endpoints (its <c>token_endpoint_auth_method</c>).
    /// </summary>
    public enum OAuthClientAuthenticationMethod
    {
        /// <summary>
        /// A public client (<c>none</c>): it holds no secret and sends only its <c>client_id</c>. PKCE is what proves an
        /// authorization code is its own. Every client identified by a metadata document is public.
        /// </summary>
        None,

        /// <summary>
        /// A confidential client sending its id and secret in an HTTP Basic <c>Authorization</c> header
        /// (<c>client_secret_basic</c>), the default RFC 7591 gives a registration that names no method.
        /// </summary>
        ClientSecretBasic,

        /// <summary>
        /// A confidential client sending <c>client_id</c> and <c>client_secret</c> in the form body (<c>client_secret_post</c>).
        /// </summary>
        ClientSecretPost,
    }
}
