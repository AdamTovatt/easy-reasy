namespace EasyReasy.Auth
{
    /// <summary>
    /// The grant type of a token request.
    /// </summary>
    public enum OAuthGrantType
    {
        /// <summary>
        /// Redeeming an authorization code (<c>authorization_code</c>).
        /// </summary>
        AuthorizationCode,

        /// <summary>
        /// Refreshing with a refresh token (<c>refresh_token</c>).
        /// </summary>
        RefreshToken,
    }
}
