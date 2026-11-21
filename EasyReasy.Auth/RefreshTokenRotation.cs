using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The one implementation of a refresh token family's life: creating it with its first token, and rotating it with
    /// reuse detection. <see cref="RefreshTokenService"/> runs it over the first-party <see cref="IRefreshTokenStore"/>,
    /// adding session policy and audit around it; the OAuth token endpoint runs it over a client's refresh tokens.
    /// </summary>
    internal sealed class RefreshTokenRotation
    {
        private readonly IRefreshTokenRotationStore _store;
        private readonly TimeSpan _refreshTokenLifetime;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes the rotation.
        /// </summary>
        /// <param name="store">The store the family's tokens live in.</param>
        /// <param name="refreshTokenLifetime">The lifetime of each refresh token.</param>
        /// <param name="accessTokenLifetime">The lifetime of each access token.</param>
        /// <param name="timeProvider">The clock.</param>
        public RefreshTokenRotation(IRefreshTokenRotationStore store, TimeSpan refreshTokenLifetime, TimeSpan accessTokenLifetime, TimeProvider timeProvider)
        {
            _store = store;
            _refreshTokenLifetime = refreshTokenLifetime;
            _accessTokenLifetime = accessTokenLifetime;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Creates a family: stores its first refresh token.
        /// </summary>
        /// <param name="familyId">The family id. The OAuth server chooses it when it issues a code, so it can invalidate
        /// whatever that code's first redemption issued if the code is presented again.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="authType">The authentication type.</param>
        /// <param name="serializedClaims">The serialized claims, or null.</param>
        /// <param name="serializedRoles">The serialized roles, or null.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The raw refresh token and the family id.</returns>
        public async Task<RefreshTokenCreationResult> CreateFamilyAsync(
            string familyId,
            string subject,
            string authType,
            string? serializedClaims,
            string? serializedRoles,
            CancellationToken cancellationToken)
        {
            string rawToken = SecureToken.Generate();
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

            StoredRefreshToken storedToken = new StoredRefreshToken
            {
                TokenHash = SecureToken.Hash(rawToken),
                Subject = subject,
                AuthType = authType,
                FamilyId = familyId,
                CreatedAt = now,
                ExpiresAt = now.Add(_refreshTokenLifetime),
                SerializedClaims = serializedClaims,
                SerializedRoles = serializedRoles
            };

            await _store.StoreAsync(storedToken, cancellationToken);
            return new RefreshTokenCreationResult { RawToken = rawToken, FamilyId = familyId };
        }

        /// <summary>
        /// Creates a family and issues its first access token the way <see cref="RotateAsync"/> issues every later one.
        /// The claims and roles come serialized, by <see cref="SerializeSessionClaims"/>, so a caller can fail on bad
        /// claims before it does anything else.
        /// </summary>
        /// <param name="familyId">The family id.</param>
        /// <param name="jwtTokenService">The issuer of the access token.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="authType">The authentication type.</param>
        /// <param name="serializedClaims">The additional claims the session's tokens carry, serialized.</param>
        /// <param name="serializedRoles">The roles the session's tokens carry, serialized.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The token pair and the family id.</returns>
        public async Task<SessionStartResult> StartSessionAsync(
            string familyId,
            IJwtTokenService jwtTokenService,
            string subject,
            string authType,
            string? serializedClaims,
            string? serializedRoles,
            CancellationToken cancellationToken)
        {
            RefreshTokenCreationResult family = await CreateFamilyAsync(familyId, subject, authType, serializedClaims, serializedRoles, cancellationToken);

            // The first token is built from the stored form, exactly what a refresh reads back, so nothing the store
            // does not keep (a claim's value type, say) can make it differ from a refreshed token.
            AuthResponse authResponse = IssueAccessToken(
                jwtTokenService,
                subject,
                authType,
                RefreshTokenClaims.DeserializeClaims(serializedClaims),
                RefreshTokenClaims.DeserializeRoles(serializedRoles),
                family.FamilyId,
                family.RawToken,
                _timeProvider.GetUtcNow().UtcDateTime);

            return new SessionStartResult { AuthResponse = authResponse, FamilyId = family.FamilyId };
        }

        /// <summary>
        /// The stored form of a session's claims: without a family id claim, since every token gets the real one from the
        /// row's family id.
        /// </summary>
        /// <param name="claims">The claims.</param>
        /// <returns>The serialized claims, or null for none.</returns>
        public static string? SerializeSessionClaims(IEnumerable<Claim> claims)
        {
            return RefreshTokenClaims.SerializeClaims(WithoutFamilyIdClaim(claims));
        }

        /// <summary>
        /// Rotates a refresh token: consumes it and issues the next pair, or refuses it. A token presented after it was
        /// used invalidates its family.
        /// </summary>
        /// <param name="refreshToken">The raw refresh token presented.</param>
        /// <param name="jwtTokenService">The issuer of the access token.</param>
        /// <param name="claimsResolver">Re-derives the claims and roles before the token is consumed, or null to keep the
        /// stored ones.</param>
        /// <param name="httpContext">The request, for the resolver.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The next pair, or why the token was refused.</returns>
        public async Task<RefreshResult> RotateAsync(
            string refreshToken,
            IJwtTokenService jwtTokenService,
            IRefreshClaimsResolver? claimsResolver,
            HttpContext? httpContext,
            CancellationToken cancellationToken)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            string tokenHash = SecureToken.Hash(refreshToken);
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
            if (claimsResolver != null)
            {
                RefreshClaimsContext context = new RefreshClaimsContext(
                    storedToken.Subject,
                    storedToken.AuthType,
                    claims,
                    roles,
                    httpContext);

                RefreshClaimsDecision decision = await claimsResolver.ResolveAsync(context, cancellationToken);

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

            string newRawToken = SecureToken.Generate();
            string newTokenHash = SecureToken.Hash(newRawToken);

            // The family id claim is added to the token only, never to the new row's serialized claims.
            AuthResponse authResponse = IssueAccessToken(
                jwtTokenService,
                storedToken.Subject,
                storedToken.AuthType,
                claims,
                roles,
                storedToken.FamilyId,
                newRawToken,
                now);

            // When the resolver ran, the new row persists the resolved claims/roles so subsequent
            // refreshes start from there; when no resolver is registered, the original serialized
            // JSON is reused verbatim.
            string? newSerializedClaims = claimsResolver != null ? RefreshTokenClaims.SerializeClaims(claims) : storedToken.SerializedClaims;
            string? newSerializedRoles = claimsResolver != null ? RefreshTokenClaims.SerializeRoles(roles) : storedToken.SerializedRoles;

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

            // The consumed token is read again now the new one is stored. A family invalidation that came in after the
            // consume above (a replay of this token, a logout, a revocation) marked the consumed token too: if it ran
            // before the store, it shows here; if after, it caught the new token as well. Either way the new token does
            // not outlive it.
            StoredRefreshToken? consumedToken = await _store.GetByTokenHashAsync(tokenHash, cancellationToken);
            if (consumedToken == null || consumedToken.IsInvalidated)
            {
                await _store.InvalidateFamilyAsync(storedToken.FamilyId, cancellationToken);
                return RefreshResult.Failed(RefreshFailureReason.TokenInvalidated, storedToken.Subject, storedToken.FamilyId);
            }

            return RefreshResult.Succeeded(authResponse, newRawToken, storedToken.Subject, storedToken.FamilyId);
        }

        /// <summary>
        /// Issues an access token for a refresh token family and wraps it with the family's raw refresh token. This is
        /// the one place both a session's first token (<see cref="StartSessionAsync"/>) and every refreshed one
        /// (<see cref="RotateAsync"/>) are made, so the two share their lifetime, expiry format and family id claim.
        /// </summary>
        /// <remarks>
        /// The family id rides on the access token as the authoritative <see cref="EasyReasyClaimTypes.RefreshFamilyId"/>
        /// claim so a re-issue endpoint can name and retire exactly this family. The value always comes from the
        /// server-side family id (copied onto every rotation's row, so it is stable across the family's lifetime), never
        /// from the claims passed in, so any such claim among them is stripped and replaced here and cannot be spoofed.
        /// </remarks>
        private AuthResponse IssueAccessToken(
            IJwtTokenService jwtTokenService,
            string subject,
            string authType,
            IEnumerable<Claim> claims,
            IEnumerable<string> roles,
            string familyId,
            string rawRefreshToken,
            DateTime now)
        {
            DateTime expiresAt = now.Add(_accessTokenLifetime);

            List<Claim> accessTokenClaims = WithoutFamilyIdClaim(claims);
            accessTokenClaims.Add(new Claim(EasyReasyClaimTypes.RefreshFamilyId, familyId));

            string accessToken = jwtTokenService.CreateToken(subject, authType, accessTokenClaims, roles, expiresAt);
            return new AuthResponse(accessToken, expiresAt.ToString("O"), rawRefreshToken);
        }

        private static List<Claim> WithoutFamilyIdClaim(IEnumerable<Claim> claims)
        {
            return claims.Where(claim => claim.Type != EasyReasyClaimTypes.RefreshFamilyId).ToList();
        }
    }
}
