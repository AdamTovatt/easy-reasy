namespace EasyReasy.Auth
{
    /// <summary>
    /// What the application can do to a grant from its own code. Registered by
    /// <see cref="OAuthServerServiceCollectionExtensions.AddEasyReasyOAuthServer{TStore, THandler}"/>.
    /// </summary>
    public interface IOAuthGrantService
    {
        /// <summary>
        /// Ends a grant: every refresh token issued under it is invalidated at once, and a code of it not yet redeemed can
        /// no longer be. Call it when the connection is revoked, the person it acts for loses their access, or its
        /// organization turns connections off.
        /// </summary>
        /// <remarks>
        /// An access token already issued under the grant stays valid until it expires
        /// (<see cref="OAuthServerOptions.AccessTokenLifetime"/>): it is a signed token checked without a lookup. An
        /// application that must stop the grant sooner checks its own grant record on requests carrying the grant's
        /// claims. Idempotent: an unknown or already revoked grant id is a no-op.
        /// </remarks>
        /// <param name="grantId">The application's grant id (<see cref="OAuthGrant.GrantId"/>).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task RevokeGrantAsync(string grantId, CancellationToken cancellationToken = default);
    }
}
