namespace EasyReasy.Auth
{
    /// <summary>
    /// Optional consumer-supplied hook invoked by <see cref="RefreshTokenService"/> on every
    /// refresh, after the stored token has been validated but before the atomic consume.
    /// </summary>
    /// <remarks>
    /// When registered in DI, the resolver's decision either allows the refresh to proceed —
    /// with the resolver's claims and roles in place of the stored ones on both the new access
    /// token and the newly persisted refresh token — or denies it without burning the stored
    /// refresh token. Resolver-before-consume ordering ensures that a transient resolver
    /// failure (deny or throw) cannot cause the legitimate next attempt to trip theft
    /// detection and revoke the user's entire session family.
    ///
    /// When no resolver is registered, refresh behaviour is identical to having no hook at
    /// all: the stored claims and roles are replayed verbatim onto the new tokens.
    ///
    /// Implementations should be resilient to a <see cref="RefreshClaimsContext.HttpContext"/>
    /// of <c>null</c>, which occurs when <see cref="IRefreshTokenService.RefreshAsync"/> is
    /// invoked from a non-HTTP caller such as a background job.
    ///
    /// Implementations should be fast. The resolver runs inside the read-then-consume window
    /// of the refresh path, so a slow resolver (for example one that issues a per-refresh
    /// network call or database round-trip) widens the window in which two near-simultaneous
    /// legitimate refreshes from the same client can race, with the loser tripping theft
    /// detection and invalidating the entire token family. Cache hot lookups; avoid expensive
    /// work on the refresh path.
    ///
    /// Resolvers should also avoid side effects on the hot path. The decision returned by the
    /// resolver is not the final outcome of the refresh: the subsequent atomic consume can
    /// still fail and return <see cref="RefreshFailureReason.TheftDetected"/>, in which case
    /// the family is invalidated but anything the resolver already wrote (audit rows, counter
    /// increments, outbound webhooks) is not rolled back. Any side effect a resolver must
    /// perform should therefore be idempotent so repeated refresh attempts after a theft
    /// rejection do not double-emit.
    /// </remarks>
    public interface IRefreshClaimsResolver
    {
        /// <summary>
        /// Re-evaluates claims and roles for a refresh request, or denies the refresh.
        /// </summary>
        /// <param name="context">The refresh context, including the originally stored claims and roles.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="RefreshClaimsDecision"/> describing the resolver's verdict. Exceptions
        /// thrown from this method propagate out of <see cref="IRefreshTokenService.RefreshAsync"/>
        /// without consuming the stored refresh token, so the client can retry once the
        /// underlying issue is resolved. Before the exception propagates, a synthetic
        /// <see cref="RefreshFailureReason.ResolverError"/> result is emitted to
        /// <see cref="IAuthAuditLogger.OnRefreshAsync"/> so the audit trail remains the
        /// authoritative record of every refresh outcome (ISO 27001 A.12.4.1). Consumers
        /// that prefer graceful failure without surfacing an exception should catch internally
        /// and return <see cref="RefreshClaimsDecision.Deny"/> instead.
        /// </returns>
        Task<RefreshClaimsDecision> ResolveAsync(RefreshClaimsContext context, CancellationToken cancellationToken);
    }
}
