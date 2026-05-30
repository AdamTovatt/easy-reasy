using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        /// Optional audit logger. When supplied, this service invokes the matching hook after every
        /// <see cref="RefreshAsync"/> (<see cref="IAuthAuditLogger.OnRefreshAsync"/>),
        /// <see cref="LogoutAsync"/> (<see cref="IAuthAuditLogger.OnLogoutAsync"/>), and
        /// <see cref="InvalidateAllSessionsAsync"/> (<see cref="IAuthAuditLogger.OnSessionsInvalidatedAsync"/>) call
        /// so consumers can emit ISO 27001 A.12.4.1 / A.9.2.6 audit records for both HTTP-driven and programmatic flows.
        /// Lifetime must be at least as long as this service — see <see cref="IAuthAuditLogger"/> remarks.
        /// </param>
        /// <param name="claimsResolver">
        /// Optional consumer-supplied claims/roles resolver. When supplied, this service invokes the
        /// resolver on every refresh — before the atomic consume — to either re-evaluate the claims
        /// and roles that ride onto the new tokens (replacing what was originally stored at login
        /// time) or deny the refresh outright. See <see cref="IRefreshClaimsResolver"/>.
        /// </param>
        public RefreshTokenService(
            IRefreshTokenStore store,
            TimeSpan? refreshTokenLifetime = null,
            TimeSpan? accessTokenLifetime = null,
            IAuthAuditLogger? auditLogger = null,
            IRefreshClaimsResolver? claimsResolver = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _refreshTokenLifetime = refreshTokenLifetime ?? TimeSpan.FromDays(30);
            _accessTokenLifetime = accessTokenLifetime ?? TimeSpan.FromHours(1);
            _auditLogger = auditLogger;
            _claimsResolver = claimsResolver;
        }

        /// <inheritdoc />
        public async Task<string> CreateRefreshTokenAsync(
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

            await _store.StoreAsync(storedToken, cancellationToken);

            return rawToken;
        }

        /// <inheritdoc />
        public async Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
        {
            RefreshResult result = await ComputeRefreshAsync(refreshToken, jwtTokenService, httpContext, cancellationToken);

            if (_auditLogger != null)
            {
                await _auditLogger.OnRefreshAsync(httpContext, result);
            }

            return result;
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
            IReadOnlyList<Claim> claims = DeserializeClaims(storedToken.SerializedClaims);
            IReadOnlyList<string> roles = DeserializeRoles(storedToken.SerializedRoles);

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
            string accessToken = jwtTokenService.CreateToken(
                storedToken.Subject,
                storedToken.AuthType,
                claims,
                roles,
                accessTokenExpiresAt);

            // When the resolver ran, the new row persists the resolved claims/roles so subsequent
            // refreshes start from there; when no resolver is registered, the original serialized
            // JSON is reused verbatim.
            string newRawToken = GenerateToken();
            string newTokenHash = HashToken(newRawToken);

            string? newSerializedClaims = _claimsResolver != null ? SerializeClaims(claims) : storedToken.SerializedClaims;
            string? newSerializedRoles = _claimsResolver != null ? SerializeRoles(roles) : storedToken.SerializedRoles;

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

            int invalidatedCount = await _store.InvalidateAllFamiliesForUserAsync(subject, cancellationToken);
            SessionRevocationResult result = new SessionRevocationResult(subject, invalidatedCount);

            if (_auditLogger != null)
            {
                await _auditLogger.OnSessionsInvalidatedAsync(result);
            }

            return result;
        }

        /// <summary>
        /// Serializes a collection of claims to a JSON string.
        /// </summary>
        /// <param name="claims">The claims to serialize.</param>
        /// <returns>A JSON string representing the claims, or null if the collection is empty.</returns>
        internal static string? SerializeClaims(IEnumerable<Claim> claims)
        {
            List<ClaimEntry> entries = claims.Select(c => new ClaimEntry(c.Type, c.Value)).ToList();

            if (entries.Count == 0)
            {
                return null;
            }

            return JsonSerializer.Serialize(entries, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Serializes a collection of roles to a JSON string.
        /// </summary>
        /// <param name="roles">The roles to serialize.</param>
        /// <returns>A JSON string representing the roles, or null if the collection is empty.</returns>
        internal static string? SerializeRoles(IEnumerable<string> roles)
        {
            List<string> roleList = roles.ToList();

            if (roleList.Count == 0)
            {
                return null;
            }

            return JsonSerializer.Serialize(roleList, JsonSerializerSettings.CurrentOptions);
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

        private static List<Claim> DeserializeClaims(string? serializedClaims)
        {
            if (string.IsNullOrEmpty(serializedClaims))
            {
                return new List<Claim>();
            }

            List<ClaimEntry>? entries = JsonSerializer.Deserialize<List<ClaimEntry>>(serializedClaims, JsonSerializerSettings.CurrentOptions);

            if (entries == null)
            {
                return new List<Claim>();
            }

            return entries.Select(e => new Claim(e.Type, e.Value)).ToList();
        }

        private static List<string> DeserializeRoles(string? serializedRoles)
        {
            if (string.IsNullOrEmpty(serializedRoles))
            {
                return new List<string>();
            }

            List<string>? roles = JsonSerializer.Deserialize<List<string>>(serializedRoles, JsonSerializerSettings.CurrentOptions);
            return roles ?? new List<string>();
        }

        /// <summary>
        /// Internal record used for JSON serialization of claims.
        /// </summary>
        private record ClaimEntry(string Type, string Value);
    }
}
