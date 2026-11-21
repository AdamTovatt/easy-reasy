namespace EasyReasy.Auth
{
    /// <summary>
    /// The application's side of the authorization server: it decides who may approve what, records the grant, and
    /// re-decides on every refresh. Registered with
    /// <see cref="OAuthServerServiceCollectionExtensions.AddEasyReasyOAuthServer{TStore, THandler}"/> and resolved per
    /// request, so it may depend on scoped services.
    /// </summary>
    public interface IOAuthGrantHandler
    {
        /// <summary>
        /// Called when the signed-in person approves a pending request on the consent page. Decide whether this person
        /// may approve this connection as the body describes it, record the grant, and return what the tokens carry, or
        /// refuse.
        /// </summary>
        /// <remarks>
        /// A refusal leaves the request pending, so the person can choose differently or deny it. After an approval the
        /// server completes the request (once) and issues the code, so two approvals of one request, a double submit say,
        /// both reach this method and only one gets a code; when completing fails (the store threw), the request stays
        /// pending and can be approved again. Record the grant under <see cref="OAuthApprovalContext.RequestId"/> and
        /// return the grant already recorded for it to the same person (refuse anyone else), so every approval of a
        /// request names one grant and none is left without tokens. Throwing fails the request with a 500 and leaves it
        /// pending.
        /// </remarks>
        /// <param name="context">The request, the client, and the application's approval body.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The approval with its grant, or a refusal.</returns>
        Task<OAuthApprovalDecision> ApproveAsync(OAuthApprovalContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Called on every refresh, before the refresh token is consumed. Re-derive the claims and roles from the grant as
        /// it stands now, or deny: a person who has lost their access, a revoked connection, a switch turned off.
        /// </summary>
        /// <remarks>
        /// Works as <see cref="IRefreshClaimsResolver.ResolveAsync"/> does for a first-party session, and is separate from
        /// it, so a resolver written for browser sessions never runs on a client's connection. A denial or a throw does not
        /// consume the refresh token. What an allow returns replaces the stored claims and roles on the new access token
        /// and the new refresh token; a <see cref="EasyReasyClaimTypes.ClientId"/> or
        /// <see cref="EasyReasyClaimTypes.RefreshFamilyId"/> claim in it is dropped and the server's own value is used.
        /// </remarks>
        /// <param name="context">The grant, the client and what the previous token carried.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns><see cref="RefreshClaimsDecision.Allow"/> with the claims and roles, or <see cref="RefreshClaimsDecision.Deny"/>.</returns>
        Task<RefreshClaimsDecision> RefreshAsync(OAuthRefreshContext context, CancellationToken cancellationToken);
    }
}
