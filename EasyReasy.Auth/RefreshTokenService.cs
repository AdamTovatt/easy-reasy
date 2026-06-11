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
        private readonly TimeSpan _refreshTokenLifetime;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly IAuthAuditLogger? _auditLogger;
        private readonly IRefreshClaimsResolver? _claimsResolver;
        private readonly ConcurrentSessionPolicy _concurrentSessionPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshTokenService"/> class.
        /// </summary>
        /// <param name="store">The consumer-implemented refresh token store.</param>
        /// <param name="refreshTokenLifetime">
        /// The lifetime of refresh tokens. Defaults to 30 days if not specified.
        /// </param>
        /// <param name="accessTokenLifetime">
        /// The lifetime of access tokens created during refresh. Defaults to 1 hour if not specified.
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
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _refreshTokenLifetime = refreshTokenLifetime ?? TimeSpan.FromDays(30);
            _accessTokenLifetime = accessTokenLifetime ?? TimeSpan.FromHours(1);
            _auditLogger = auditLogger;
            _claimsResolver = claimsResolver;
            _concurrentSessionPolicy = concurrentSessionPolicy;
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
            string rawToken = GenerateToken();
            string tokenHash = HashToken(rawToken);
            string familyId = Guid.NewGuid().ToString();
            DateTime now = DateTime.UtcNow;

            StoredRefreshToken storedToken = new StoredRefreshToken
            {
                TokenHash = tokenHash,
                Subject = subject,
                AuthType = authType,
                FamilyId = familyId,
                CreatedAt = now,
                ExpiresAt = now.Add(_refreshTokenLifetime),
                SerializedClaims = serializedClaims,
                SerializedRoles = serializedRoles
            };

            int invalidatedFamilyCount = await EnforceSingleSessionPolicyAsync(subject, cancellationToken);

            await _store.StoreAsync(storedToken, cancellationToken);

            // Fire the audit hook only after the new family is persisted, matching the other hook sites
            // (OnRefreshAsync / OnLogoutAsync / OnSessionsInvalidatedAsync), which all fire after their
            // mutation lands. A throwing logger then leaves a stored session rather than a session-less
            // subject, and the "concurrent sessions revoked" record only fires for a login that completed.
            if (invalidatedFamilyCount > 0 && _auditLogger != null)
            {
                await _auditLogger.OnConcurrentSessionsRevokedAsync(new SessionRevocationResult(subject, invalidatedFamilyCount));
            }

            return new RefreshTokenCreationResult { RawToken = rawToken, FamilyId = familyId };
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
                result = await ComputeRefreshAsync(refreshToken, jwtTokenService, httpContext, cancellationToken);
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
                StoredRefreshToken? storedToken = await _store.GetByTokenHashAsync(HashToken(refreshToken), cancellationToken);
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

        private async Task<RefreshResult> ComputeRefreshAsync(string refreshToken, IJwtTokenService jwtTokenService, HttpContext? httpContext, CancellationToken cancellationToken)
        {
            DateTime now = DateTime.UtcNow;
            string tokenHash = HashToken(refreshToken);
            StoredRefreshToken? storedToken = await _store.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (storedToken == null)
            {
                return RefreshResult.Failed(RefreshFailureReason.TokenNotFound);
            }

            if (storedToken.IsInvalidated)
            {
                return RefreshResult.Failed(RefreshFailureReason.TokenInvalidated, storedToken.Subject, storedToken.FamilyId);
            }

            // Token was already consumed — theft detected.
            if (storedToken.ConsumedAt != null)
            {
                await _store.InvalidateFamilyAsync(storedToken.FamilyId, cancellationToken);
                return RefreshResult.Failed(RefreshFailureReason.TheftDetected, storedToken.Subject, storedToken.FamilyId);
            }

            if (storedToken.ExpiresAt <= now)
            {
                return RefreshResult.Failed(RefreshFailureReason.TokenExpired, storedToken.Subject, storedToken.FamilyId);
            }

            // Deserialize once — both the no-resolver branch and the resolver-input branch need these.
            IReadOnlyList<Claim> claims = RefreshTokenClaims.DeserializeClaims(storedToken.SerializedClaims);
            IReadOnlyList<string> roles = RefreshTokenClaims.DeserializeRoles(storedToken.SerializedRoles);

            // Invoke the consumer-supplied resolver, if registered. This runs BEFORE the atomic
            // consume so that a deny or a thrown resolver does not burn the stored refresh
            // token — a transient failure would otherwise trip theft detection on the legitimate
            // retry and invalidate the entire session family.
            if (_claimsResolver != null)
            {
                RefreshClaimsContext context = new RefreshClaimsContext(
                    storedToken.Subject,
                    storedToken.AuthType,
                    claims,
                    roles,
                    httpContext);

                RefreshClaimsDecision decision = await _claimsResolver.ResolveAsync(context, cancellationToken);

                if (decision.IsDenied)
                {
                    return RefreshResult.Failed(RefreshFailureReason.DeniedByResolver, storedToken.Subject, storedToken.FamilyId);
                }

                claims = decision.Claims;
                roles = decision.Roles;
            }

            // Atomically mark old token as consumed — if another request already consumed it, treat as theft.
            bool consumed = await _store.MarkAsConsumedAsync(tokenHash, now, cancellationToken);
            if (!consumed)
            {
                await _store.InvalidateFamilyAsync(storedToken.FamilyId, cancellationToken);
                return RefreshResult.Failed(RefreshFailureReason.TheftDetected, storedToken.Subject, storedToken.FamilyId);
            }

            DateTime accessTokenExpiresAt = now.Add(_accessTokenLifetime);

            // The family id rides on the access token as the authoritative "family_id" claim so a re-issue
            // endpoint can name and retire exactly this prior family. The value always comes from
            // storedToken.FamilyId (copied onto every rotation's row, so it is stable across the family's
            // lifetime) — never from caller-supplied stored claims — so any value already present is stripped
            // and replaced here, and it cannot be spoofed via SerializedClaims. Built as a separate list so it
            // is not persisted into the new row's serialized claims; the re-injection above is the single source.
            List<Claim> accessTokenClaims = claims.Where(claim => claim.Type != "family_id").ToList();
            accessTokenClaims.Add(new Claim("family_id", storedToken.FamilyId));

            string accessToken = jwtTokenService.CreateToken(
                storedToken.Subject,
                storedToken.AuthType,
                accessTokenClaims,
                roles,
                accessTokenExpiresAt);

            // When the resolver ran, the new row persists the resolved claims/roles so subsequent
            // refreshes start from there; when no resolver is registered, the original serialized
            // JSON is reused verbatim.
            string newRawToken = GenerateToken();
            string newTokenHash = HashToken(newRawToken);

            string? newSerializedClaims = _claimsResolver != null ? RefreshTokenClaims.SerializeClaims(claims) : storedToken.SerializedClaims;
            string? newSerializedRoles = _claimsResolver != null ? RefreshTokenClaims.SerializeRoles(roles) : storedToken.SerializedRoles;

            StoredRefreshToken newStoredToken = new StoredRefreshToken
            {
                TokenHash = newTokenHash,
                Subject = storedToken.Subject,
                AuthType = storedToken.AuthType,
                FamilyId = storedToken.FamilyId,
                CreatedAt = now,
                ExpiresAt = now.Add(_refreshTokenLifetime),
                SerializedClaims = newSerializedClaims,
                SerializedRoles = newSerializedRoles,
            };

            await _store.StoreAsync(newStoredToken, cancellationToken);

            AuthResponse authResponse = new AuthResponse(
                accessToken,
                accessTokenExpiresAt.ToString("O"),
                newRawToken);

            return RefreshResult.Succeeded(authResponse, newRawToken, storedToken.Subject, storedToken.FamilyId);
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

            string tokenHash = HashToken(refreshToken);
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
            // HttpContext.GetRefreshFamilyId() directly (it is null for access tokens minted before the claim
            // existed) without a store call, an audit record, or a throw.
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

        /// <summary>
        /// Computes the SHA-256 hash of a raw refresh token.
        /// </summary>
        /// <param name="token">The raw refresh token.</param>
        /// <returns>The lowercase hexadecimal SHA-256 hash.</returns>
        internal static string HashToken(string token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string GenerateToken()
        {
            byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(tokenBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
