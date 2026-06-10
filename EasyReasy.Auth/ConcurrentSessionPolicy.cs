namespace EasyReasy.Auth
{
    /// <summary>
    /// Controls how many concurrent refresh-token families a single subject may hold at once.
    /// A "family" is one login session that survives across refresh-token rotation, so the policy
    /// effectively bounds the number of simultaneous logins per credential.
    /// </summary>
    public enum ConcurrentSessionPolicy
    {
        /// <summary>
        /// No limit. Each new login mints an independent session that coexists with any existing
        /// ones. This is the default.
        /// </summary>
        AllowMultiple = 0,

        /// <summary>
        /// At most one live session per subject (see remarks for the concurrency caveat). Creating a new
        /// refresh-token family (i.e. a login) first invalidates every existing non-invalidated family for
        /// that subject, so the newest login wins and any earlier sessions are revoked — their next refresh
        /// fails and any still-live access token expires within its short lifetime. Use this to enforce
        /// "no shared accounts" / single-session requirements (e.g. EU GMP Annex 11,
        /// 21 CFR Part 11 §11.200).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Enforcement is best-effort, not transactional: the existing families are invalidated and the new
        /// family is stored in two separate <see cref="IRefreshTokenStore"/> calls. Two logins for the same
        /// subject racing concurrently can therefore each miss the other's not-yet-stored family and both end
        /// up live. A store that needs a hard guarantee must serialize concurrent logins for the same subject
        /// (e.g. a per-subject lock or a unique constraint) — the library cannot, because
        /// <see cref="IRefreshTokenStore"/> exposes no atomic store-and-invalidate-others primitive.
        /// </para>
        /// <para>
        /// The invalidation runs before the store so the new login is never caught by its own bulk
        /// invalidation. A consequence is that if the store call fails after the invalidation succeeds, the
        /// subject is left with no live session — fail-secure, and self-correcting on the next login. That
        /// same window also means the revocation may go unaudited: <see cref="IAuthAuditLogger.OnConcurrentSessionsRevokedAsync"/>
        /// fires only after the new family is stored, so a failed store skips it.
        /// </para>
        /// </remarks>
        SingleSession = 1,
    }
}
