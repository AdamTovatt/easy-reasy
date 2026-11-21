namespace EasyReasy.Auth
{
    /// <summary>
    /// Why the token endpoint refused a request, as <see cref="OAuthTokenResult.FailureReason"/> reports it. The client is
    /// told only the OAuth error code; the reason is for the audit record.
    /// </summary>
    public enum OAuthTokenFailureReason
    {
        /// <summary>
        /// A required parameter was missing or malformed (<c>invalid_request</c>).
        /// </summary>
        InvalidRequest,

        /// <summary>
        /// The grant type is neither <c>authorization_code</c> nor <c>refresh_token</c> (<c>unsupported_grant_type</c>).
        /// </summary>
        UnsupportedGrantType,

        /// <summary>
        /// The client is unknown, or its authentication failed or used a method other than the one it registered
        /// (<c>invalid_client</c>).
        /// </summary>
        InvalidClient,

        /// <summary>
        /// No authorization code has that value.
        /// </summary>
        CodeNotFound,

        /// <summary>
        /// The code was presented again after it was redeemed. Whatever the first redemption issued is invalidated, as
        /// OAuth 2.1 requires, since one of the two presenters is not the client.
        /// </summary>
        CodeReused,

        /// <summary>
        /// The code has expired.
        /// </summary>
        CodeExpired,

        /// <summary>
        /// The code was invalidated: its grant was revoked, or it was presented a second time while this redemption was
        /// under way.
        /// </summary>
        CodeInvalidated,

        /// <summary>
        /// The code was issued to another client.
        /// </summary>
        ClientMismatch,

        /// <summary>
        /// The token request's <c>redirect_uri</c> is not the authorization request's.
        /// </summary>
        RedirectUriMismatch,

        /// <summary>
        /// The <c>code_verifier</c> does not hash to the authorization request's code challenge.
        /// </summary>
        VerifierMismatch,

        /// <summary>
        /// The token request's <c>resource</c> is not the authorization request's, or not this server's
        /// (<c>invalid_target</c>).
        /// </summary>
        ResourceMismatch,

        /// <summary>
        /// No refresh token bound to this client has that value.
        /// </summary>
        RefreshTokenNotFound,

        /// <summary>
        /// The refresh token has expired.
        /// </summary>
        RefreshTokenExpired,

        /// <summary>
        /// The refresh token's family or grant was invalidated.
        /// </summary>
        RefreshTokenInvalidated,

        /// <summary>
        /// The refresh token was presented again after it was used, so its whole family was invalidated.
        /// </summary>
        RefreshTokenReused,

        /// <summary>
        /// <see cref="IOAuthGrantHandler.RefreshAsync"/> denied the refresh.
        /// </summary>
        DeniedByHandler,

        /// <summary>
        /// <see cref="IOAuthGrantHandler.RefreshAsync"/> threw. The exception propagates after this is reported.
        /// </summary>
        HandlerError,
    }
}
