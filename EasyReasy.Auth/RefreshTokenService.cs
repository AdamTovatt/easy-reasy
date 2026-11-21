using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Core implementation of <see cref="IRefreshTokenService"/> that handles refresh token
    /// creation, rotation, and theft detection using token family tracking.
    /// </summary>
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IRefreshTokenStore _store;
        private readonly IAuthAuditLogger? _auditLogger;
        private readonly IRefreshClaimsResolver? _claimsResolver;
        private readonly ConcurrentSessionPolicy _concurrentSessionPolicy;
        private readonly RefreshTokenRotation _rotation;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshTokenService"/> class that reads the system clock.
        /// </summary>
        /// <param name="store">The consumer-implemented refresh token store.</param>
        /// <param name="refreshTokenLifetime">
        /// The lifetime of refresh tokens. Defaults to 30 days if not specified.
        /// </param>
        /// <param name="accessTokenLifetime">
        /// The lifetime of the access tokens this service issues, a session's first (<see cref="IRefreshTokenService.StartSessionAsync"/>) and every refreshed one. Defaults to 1 hour if not specified.
        /// </param>
        /// <param name="auditLogger">
        /// Optional audit logger. When supplied, this service invokes the matching service-layer hook after the
        /// corresponding operation completes, so consumers can emit ISO 27001 A.12.4.1 / A.9.2.6 audit records for
        /// both HTTP-driven and programmatic flows. See <see cref="IAuthAuditLogger"/> for the full set of hooks
        /// and which fire from the service versus the endpoint layer. Lifetime must be at least as long as this
        /// service — see <see cref="IAuthAuditLogger"/> remarks.
        /// </param>
        /// <param name="claimsResolver">
        /// Optional consumer-supplied claims/roles resolver. When supplied, this service invokes the
        /// resolver on every refresh — before the atomic consume — to either re-evaluate the claims
        /// and roles that ride onto the new tokens (replacing what was originally stored at login
        /// time) or deny the refresh outright. See <see cref="IRefreshClaimsResolver"/>.
        /// </param>
        /// <param name="concurrentSessionPolicy">
        /// How many concurrent sessions a single subject may hold. Defaults to
        /// <see cref="ConcurrentSessionPolicy.AllowMultiple"/> (no limit).
        /// <see cref="ConcurrentSessionPolicy.SingleSession"/> makes <see cref="CreateRefreshTokenAsync"/>
        /// revoke every existing family for the subject before storing the new one, so the newest
        /// login is the only live session. See <see cref="ConcurrentSessionPolicy"/>.
        /// </param>
        public RefreshTokenService(
            IRefreshTokenStore store,
            TimeSpan? refreshTokenLifetime = null,
            TimeSpan? accessTokenLifetime = null,
            IAuthAuditLogger? auditLogger = null,
            IRefreshClaimsResolver? claimsResolver = null,
            ConcurrentSessionPolicy concurrentSessionPolicy = ConcurrentSessionPolicy.AllowMultiple)
            : this(store, refreshTokenLifetime, accessTokenLifetime, auditLogger, claimsResolver, concurrentSessionPolicy, TimeProvider.System)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshTokenService"/> class that reads the given clock.
        /// Every parameter is explicit; the parameters shared with the other constructor mean the same here.
        /// </summary>
        /// <param name="store">The consumer-implemented refresh token store.</param>
        /// <param name="refreshTokenLifetime">The lifetime of refresh tokens, or null for 30 days.</param>
        /// <param name="accessTokenLifetime">The lifetime of the access tokens this service issues, or null for 1 hour.</param>
        /// <param name="auditLogger">Optional audit logger; see the other constructor.</param>
        /// <param name="claimsResolver">Optional claims/roles resolver; see the other constructor.</param>
        /// <param name="concurrentSessionPolicy">How many concurrent sessions a single subject may hold.</param>
        /// <param name="timeProvider">
        /// The clock that stamps a refresh token's <c>CreatedAt</c> and <c>ExpiresAt</c>, decides whether a presented
        /// refresh token has expired, and sets the expiry of the access tokens this service issues.
        /// <c>AddRefreshTokenService</c> passes the <see cref="TimeProvider"/> registered in DI, which token issuing and
        /// bearer validation also read.
        /// </param>
        public RefreshTokenService(
            IRefreshTokenStore store,
            TimeSpan? refreshTokenLifetime,
            TimeSpan? accessTokenLifetime,
            IAuthAuditLogger? auditLogger,
            IRefreshClaimsResolver? claimsResolver,
            ConcurrentSessionPolicy concurrentSessionPolicy,
            TimeProvider timeProvider)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _auditLogger = auditLogger;
            _claimsResolver = claimsResolver;
            _concurrentSessionPolicy = concurrentSessionPolicy;
            _rotation = new RefreshTokenRotation(
                store,
                refreshTokenLifetime ?? TimeSpan.FromDays(30),
                accessTokenLifetime ?? TimeSpan.FromHours(1),
                timeProvider ?? throw new ArgumentNullException(nameof(timeProvider)));
        }

        /// <inheritdoc />
        public async Task<string> CreateRefreshTokenAsync(
            string subject,
            string authType,
            string? serializedClaims,
            string? serializedRoles,
            CancellationToken cancellationToken = default)
        {
            // Delegate to the detailed overload so there is exactly one creation code path; this method
            // simply discards the family id that existing callers do not need.
            RefreshTokenCreationResult result = await CreateRefreshTokenWithFamilyAsync(subject, authType, serializedClaims, serializedRoles, cancellationToken);
            return result.RawToken;
        }

        /// <inheritdoc />
        public async Task<RefreshTokenCreationResult> CreateRefreshTokenWithFamilyAsync(
            string subject,
            string authType,
            string? serializedClaims,
            string? serializedRoles,
            CancellationToken cancellationToken = default)
        {
            int invalidatedFamilyCount = await EnforceSingleSessionPolicyAsync(subject, cancellationToken);
            RefreshTokenCreationResult result = await _rotation.CreateFamilyAsync(Guid.NewGuid().ToString(), subject, authType, serializedClaims, serializedRoles, cancellationToken);
            await ReportConcurrentSessionsRevokedAsync(subject, invalidatedFamilyCount);
            return result;
        }

        // Fire the audit hook only after the new family is persisted, matching the other hook sites
        // (OnRefreshAsync / OnLogoutAsync / OnSessionsInvalidatedAsync), which all fire after their
        // mutation lands. A throwing logger then leaves a stored session rather than a session-less
        // subject, and the "concurrent sessions revoked" record only fires for a login that completed.
        private async Task ReportConcurrentSessionsRevokedAsync(string subject, int invalidatedFamilyCount)
        {
            if (invalidatedFamilyCount > 0 && _auditLogger != null)
            {
                await _auditLogger.OnConcurrentSessionsRevokedAsync(new SessionRevocationResult(subject, invalidatedFamilyCount));
            }
        }

        /// <inheritdoc />
        public async Task<SessionStartResult> StartSessionAsync(
            IJwtTokenService jwtTokenService,
            string subject,
            string authType,
            IEnumerable<Claim> claims,
            IEnumerable<string> roles,
            CancellationToken cancellationToken = default)
        {
            // Checked and serialized before the session policy runs, so a bad call never revokes the subject's other
            // sessions.
            ArgumentNullException.ThrowIfNull(jwtTokenService);
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);
            ArgumentException.ThrowIfNullOrWhiteSpace(authType);
            ArgumentNullException.ThrowIfNull(claims);
            ArgumentNullException.ThrowIfNull(roles);
            string? serializedClaims = RefreshTokenRotation.SerializeSessionClaims(claims);
            string? serializedRoles = RefreshTokenClaims.SerializeRoles(roles);

            int invalidatedFamilyCount = await EnforceSingleSessionPolicyAsync(subject, cancellationToken);
            SessionStartResult session = await _rotation.StartSessionAsync(Guid.NewGuid().ToString(), jwtTokenService, subject, authType, serializedClaims, serializedRoles, cancellationToken);
            await ReportConcurrentSessionsRevokedAsync(subject, invalidatedFamilyCount);
            return session;
        }

        /// <summary>
        /// When the service is configured with <see cref="ConcurrentSessionPolicy.SingleSession"/>, invalidates
        /// every existing family for <paramref name="subject"/> so the login about to be stored becomes the only
        /// live session. Runs before the new family is stored, so that family is never caught by its own bulk
        /// invalidation. Returns the number of families invalidated (0 under
        /// <see cref="ConcurrentSessionPolicy.AllowMultiple"/>), which the caller surfaces to
        /// <see cref="IAuthAuditLogger.OnConcurrentSessionsRevokedAsync"/> after the new family is stored.
        /// </summary>
        private async Task<int> EnforceSingleSessionPolicyAsync(string subject, CancellationToken cancellationToken)
        {
            if (_concurrentSessionPolicy != ConcurrentSessionPolicy.SingleSession)
            {
                return 0;
            }

            return await _store.InvalidateAllFamiliesForUserAsync(subject, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
        {
            RefreshResult result;
            try
            {
                result = await _rotation.RotateAsync(refreshToken, jwtTokenService, _claimsResolver, httpContext, cancellationToken);
            }
            catch
            {
                // The audit trail must record every refresh outcome including resolver faults
                // (ISO 27001 A.12.4.1). Emit a synthetic ResolverError result, then rethrow so
                // the original exception still propagates with its original stack trace.
                await TryLogResolverFaultAsync(refreshToken, httpContext, cancellationToken);
                throw;
            }

            if (_auditLogger != null)
            {
                await _auditLogger.OnRefreshAsync(httpContext, result);
            }

            return result;
        }

        private async Task TryLogResolverFaultAsync(string refreshToken, HttpContext? httpContext, CancellationToken cancellationToken)
        {
            if (_auditLogger == null)
            {
                return;
            }

            // Best-effort: a fault here must never replace the original exception. If the
            // store lookup or the logger itself throws, swallow and fall through to the
            // outer rethrow so the consumer still sees the resolver's exception.
            try
            {
                string? subject = null;
                string? familyId = null;
                StoredRefreshToken? storedToken = await _store.GetByTokenHashAsync(SecureToken.Hash(refreshToken), cancellationToken);
                if (storedToken != null)
                {
                    subject = storedToken.Subject;
                    familyId = storedToken.FamilyId;
                }

                RefreshResult faultResult = RefreshResult.Failed(RefreshFailureReason.ResolverError, subject, familyId);
                await _auditLogger.OnRefreshAsync(httpContext, faultResult);
            }
            catch
            {
                // Intentionally swallowed — the outer catch's rethrow is the source of truth.
            }
        }

        /// <inheritdoc />
        public async Task<LogoutResult> LogoutAsync(string? refreshToken, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
        {
            LogoutResult result = await ComputeLogoutAsync(refreshToken, cancellationToken);

            if (_auditLogger != null)
            {
                await _auditLogger.OnLogoutAsync(httpContext, result);
            }

            return result;
        }

        private async Task<LogoutResult> ComputeLogoutAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            // A null or empty token can't map to any family — treat as a no-op so that
            // callers (including the unauthenticated HTTP endpoint) still see idempotent
            // 204 behaviour instead of a 500 from a hashing exception.
            if (string.IsNullOrEmpty(refreshToken))
            {
                return LogoutResult.Unknown();
            }

            string tokenHash = SecureToken.Hash(refreshToken);
            StoredRefreshToken? storedToken = await _store.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (storedToken == null)
            {
                return LogoutResult.Unknown();
            }

            await _store.InvalidateFamilyAsync(storedToken.FamilyId, cancellationToken);
            return LogoutResult.Known(storedToken.Subject, storedToken.FamilyId);
        }

        /// <inheritdoc />
        public async Task<SessionRevocationResult> InvalidateAllSessionsAsync(string subject, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            int invalidatedFamilyCount = await _store.InvalidateAllFamiliesForUserAsync(subject, cancellationToken);
            SessionRevocationResult result = new SessionRevocationResult(subject, invalidatedFamilyCount);

            if (_auditLogger != null)
            {
                await _auditLogger.OnSessionsInvalidatedAsync(result);
            }

            return result;
        }

        /// <inheritdoc />
        public async Task RetireFamilyAsync(string? familyId, string? subject = null, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
        {
            // A null/whitespace family id can't name any family — no-op so a re-issue endpoint can pass
            // HttpContext.GetRefreshFamilyId() directly (it is null for an access token issued without a refresh
            // token family) without a store call, an audit record, or a throw.
            if (string.IsNullOrWhiteSpace(familyId))
            {
                return;
            }

            await _store.InvalidateFamilyAsync(familyId, cancellationToken);

            // Fire after the retire lands, matching the other hook sites. The store reports neither existence
            // nor a subject, so the record means "a retire was requested for this family"; subject rides along
            // only for the audit row.
            if (_auditLogger != null)
            {
                await _auditLogger.OnSessionSupersededAsync(httpContext, new FamilyRetirementResult { FamilyId = familyId, Subject = subject });
            }
        }
    }
}
