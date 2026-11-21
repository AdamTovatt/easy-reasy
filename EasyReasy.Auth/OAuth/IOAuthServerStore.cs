namespace EasyReasy.Auth
{
    /// <summary>
    /// The application's storage for the OAuth authorization server: clients, pending authorization requests,
    /// authorization codes and refresh tokens. The library stays database-agnostic, as with
    /// <see cref="IRefreshTokenStore"/>. Every secret value (request id, code, refresh token, client secret) reaches the
    /// store only as its SHA-256 hash.
    /// </summary>
    /// <remarks>
    /// The three methods that return <c>bool</c> must be atomic compare-and-set operations that succeed for the first
    /// caller only (in SQL, an <c>UPDATE ... WHERE completed_at IS NULL</c> and a check that one row was affected): they
    /// are what makes an approval, a code and a refresh token usable once. Every method's write must be committed when
    /// its task completes: the server orders separate calls (it marks a code invalidated before it invalidates the
    /// refresh tokens, and checks the code again after storing a new refresh token), and that order is what keeps a
    /// redemption racing a revocation or a second redemption from leaving a token alive. Rows past their
    /// <c>ExpiresAt</c> are never accepted, so deleting them is housekeeping the application schedules as it likes.
    /// </remarks>
    public interface IOAuthServerStore
    {
        /// <summary>
        /// Stores a new client.
        /// </summary>
        /// <param name="client">The client to store.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task StoreClientAsync(OAuthClient client, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a client by its id.
        /// </summary>
        /// <param name="clientId">The client id, compared exactly (ordinal).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The client, or null if none has that id.</returns>
        Task<OAuthClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a new pending authorization request.
        /// </summary>
        /// <param name="request">The request to store.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task StoreAuthorizationRequestAsync(OAuthAuthorizationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an authorization request by the hash of its id.
        /// </summary>
        /// <param name="requestHash">The SHA-256 hash of the request id.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The request, completed or not, or null if none has that hash.</returns>
        Task<OAuthAuthorizationRequest?> GetAuthorizationRequestAsync(string requestHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically marks an authorization request as completed (approved or denied).
        /// </summary>
        /// <param name="requestHash">The SHA-256 hash of the request id.</param>
        /// <param name="completedAt">The UTC time of the completion.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True for the first caller only; false if the request was already completed or does not exist.</returns>
        Task<bool> CompleteAuthorizationRequestAsync(string requestHash, DateTime completedAt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a new authorization code.
        /// </summary>
        /// <param name="code">The code to store.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task StoreAuthorizationCodeAsync(OAuthAuthorizationCode code, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an authorization code by its hash.
        /// </summary>
        /// <param name="codeHash">The SHA-256 hash of the raw code.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The code, redeemed or not, or null if none has that hash.</returns>
        Task<OAuthAuthorizationCode?> GetAuthorizationCodeAsync(string codeHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically marks an authorization code as redeemed.
        /// </summary>
        /// <param name="codeHash">The SHA-256 hash of the raw code.</param>
        /// <param name="redeemedAt">The UTC time of the redemption.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True for the first caller only; false if the code was already redeemed or does not exist.</returns>
        Task<bool> RedeemAuthorizationCodeAsync(string codeHash, DateTime redeemedAt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a new refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token to store.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task StoreRefreshTokenAsync(OAuthRefreshToken refreshToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a refresh token by its hash.
        /// </summary>
        /// <param name="tokenHash">The SHA-256 hash of the raw refresh token.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The refresh token, or null if none has that hash.</returns>
        Task<OAuthRefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically marks a refresh token as consumed (used to obtain the next pair).
        /// </summary>
        /// <param name="tokenHash">The SHA-256 hash of the raw refresh token.</param>
        /// <param name="consumedAt">The UTC time of the consumption.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True for the first caller only; false if the token was already consumed or does not exist.</returns>
        Task<bool> ConsumeRefreshTokenAsync(string tokenHash, DateTime consumedAt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates every refresh token in a family: on reuse of a consumed token, on a second redemption of the
        /// code the family came from, or on revocation by the client.
        /// </summary>
        /// <param name="familyId">The family to invalidate.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task InvalidateRefreshTokenFamilyAsync(string familyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets <see cref="OAuthAuthorizationCode.IsInvalidated"/> on one code, redeemed or not: the server does this when
        /// a code is presented a second time, before it invalidates the code's family.
        /// </summary>
        /// <param name="codeHash">The SHA-256 hash of the raw code.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task InvalidateAuthorizationCodeAsync(string codeHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets <see cref="OAuthAuthorizationCode.IsInvalidated"/> on every code whose
        /// <see cref="OAuthAuthorizationCode.GrantId"/> is <paramref name="grantId"/>, redeemed or not.
        /// </summary>
        /// <param name="grantId">The application's grant id.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task InvalidateGrantAuthorizationCodesAsync(string grantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates every refresh token whose <see cref="OAuthRefreshToken.GrantId"/> is <paramref name="grantId"/>.
        /// </summary>
        /// <param name="grantId">The application's grant id.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        Task InvalidateGrantRefreshTokensAsync(string grantId, CancellationToken cancellationToken = default);
    }
}
