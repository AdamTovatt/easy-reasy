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
        public RefreshTokenService(
            IRefreshTokenStore store,
            TimeSpan? refreshTokenLifetime = null,
            TimeSpan? accessTokenLifetime = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _refreshTokenLifetime = refreshTokenLifetime ?? TimeSpan.FromDays(30);
            _accessTokenLifetime = accessTokenLifetime ?? TimeSpan.FromHours(1);
        }

        /// <inheritdoc />
        public async Task<string> CreateRefreshTokenAsync(
            string subject,
            string authType,
            string? serializedClaims,
            string? serializedRoles)
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

            await _store.StoreAsync(storedToken);

            return rawToken;
        }

        /// <inheritdoc />
        public async Task<RefreshResult> RefreshAsync(string refreshToken, IJwtTokenService jwtTokenService)
        {
            DateTime now = DateTime.UtcNow;
            string tokenHash = HashToken(refreshToken);
            StoredRefreshToken? storedToken = await _store.GetByTokenHashAsync(tokenHash);

            // Step 1: Token not found
            if (storedToken == null)
            {
                return RefreshResult.Failed(RefreshFailureReason.TokenNotFound);
            }

            // Step 2: Token invalidated
            if (storedToken.IsInvalidated)
            {
                return RefreshResult.Failed(RefreshFailureReason.TokenInvalidated);
            }

            // Step 3: Token already consumed — theft detected
            if (storedToken.ConsumedAt != null)
            {
                await _store.InvalidateFamilyAsync(storedToken.FamilyId);
                return RefreshResult.Failed(RefreshFailureReason.TheftDetected);
            }

            // Step 4: Token expired
            if (storedToken.ExpiresAt <= now)
            {
                return RefreshResult.Failed(RefreshFailureReason.TokenExpired);
            }

            // Step 5: Atomically mark old token as consumed — if another request already consumed it, treat as theft
            bool consumed = await _store.MarkAsConsumedAsync(tokenHash, now);
            if (!consumed)
            {
                await _store.InvalidateFamilyAsync(storedToken.FamilyId);
                return RefreshResult.Failed(RefreshFailureReason.TheftDetected);
            }

            // Step 6: Deserialize stored claims and roles, create new access token
            List<Claim> claims = DeserializeClaims(storedToken.SerializedClaims);
            List<string> roles = DeserializeRoles(storedToken.SerializedRoles);

            DateTime accessTokenExpiresAt = now.Add(_accessTokenLifetime);
            string accessToken = jwtTokenService.CreateToken(
                storedToken.Subject,
                storedToken.AuthType,
                claims,
                roles,
                accessTokenExpiresAt);

            // Step 7: Generate new refresh token in same family
            string newRawToken = GenerateToken();
            string newTokenHash = HashToken(newRawToken);

            StoredRefreshToken newStoredToken = new StoredRefreshToken
            {
                TokenHash = newTokenHash,
                Subject = storedToken.Subject,
                AuthType = storedToken.AuthType,
                FamilyId = storedToken.FamilyId,
                CreatedAt = now,
                ExpiresAt = now.Add(_refreshTokenLifetime),
                SerializedClaims = storedToken.SerializedClaims,
                SerializedRoles = storedToken.SerializedRoles
            };

            await _store.StoreAsync(newStoredToken);

            // Step 8: Return success with new token pair
            AuthResponse authResponse = new AuthResponse(
                accessToken,
                accessTokenExpiresAt.ToString("O"),
                newRawToken);

            return RefreshResult.Succeeded(authResponse, newRawToken);
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
