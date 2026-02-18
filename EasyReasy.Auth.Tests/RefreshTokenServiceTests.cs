using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class RefreshTokenServiceTests
    {
        private const string TestSecret = "super_secret_key_12345_12345_12345";
        private const string TestIssuer = "test-issuer";

        private FakeRefreshTokenStore _store = null!;
        private RefreshTokenService _service = null!;
        private IJwtTokenService _jwtTokenService = null!;

        [TestInitialize]
        public void Setup()
        {
            _store = new FakeRefreshTokenStore();
            _service = new RefreshTokenService(_store);
            _jwtTokenService = new JwtTokenService(TestSecret, TestIssuer);
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_WithValidParameters_ShouldStoreTokenAndReturnRawToken()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);

            Assert.IsFalse(string.IsNullOrEmpty(rawToken));
            Assert.AreEqual(1, _store.Tokens.Count);

            string hash = RefreshTokenService.HashToken(rawToken);
            Assert.IsTrue(_store.Tokens.ContainsKey(hash));

            StoredRefreshToken stored = _store.Tokens[hash];
            Assert.AreEqual("user-1", stored.Subject);
            Assert.AreEqual("user", stored.AuthType);
            Assert.IsNull(stored.ConsumedAt);
            Assert.IsFalse(stored.IsInvalidated);
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_ShouldGenerateUniqueFamilyIds()
        {
            string token1 = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string token2 = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);

            string hash1 = RefreshTokenService.HashToken(token1);
            string hash2 = RefreshTokenService.HashToken(token2);

            Assert.AreNotEqual(_store.Tokens[hash1].FamilyId, _store.Tokens[hash2].FamilyId);
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_ShouldSetCorrectExpiration()
        {
            TimeSpan customLifetime = TimeSpan.FromDays(7);
            RefreshTokenService service = new RefreshTokenService(_store, refreshTokenLifetime: customLifetime);

            DateTime before = DateTime.UtcNow;
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            DateTime after = DateTime.UtcNow;

            string hash = RefreshTokenService.HashToken(rawToken);
            StoredRefreshToken stored = _store.Tokens[hash];

            Assert.IsTrue(stored.ExpiresAt >= before.Add(customLifetime));
            Assert.IsTrue(stored.ExpiresAt <= after.Add(customLifetime));
        }

        [TestMethod]
        public async Task RefreshAsync_WithValidToken_ShouldReturnNewTokenPair()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);

            RefreshResult result = await _service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.AuthResponse);
            Assert.IsFalse(string.IsNullOrEmpty(result.AuthResponse.Token));
            Assert.IsFalse(string.IsNullOrEmpty(result.AuthResponse.ExpiresAt));
            Assert.IsFalse(string.IsNullOrEmpty(result.NewRefreshToken));
            Assert.AreNotEqual(rawToken, result.NewRefreshToken);
        }

        [TestMethod]
        public async Task RefreshAsync_WithValidToken_ShouldMarkOldTokenAsConsumed()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string oldHash = RefreshTokenService.HashToken(rawToken);

            await _service.RefreshAsync(rawToken, _jwtTokenService);

            StoredRefreshToken oldToken = _store.Tokens[oldHash];
            Assert.IsNotNull(oldToken.ConsumedAt);
        }

        [TestMethod]
        public async Task RefreshAsync_WithValidToken_ShouldPreserveFamilyId()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string oldHash = RefreshTokenService.HashToken(rawToken);
            string originalFamilyId = _store.Tokens[oldHash].FamilyId;

            RefreshResult result = await _service.RefreshAsync(rawToken, _jwtTokenService);

            string newHash = RefreshTokenService.HashToken(result.NewRefreshToken!);
            Assert.AreEqual(originalFamilyId, _store.Tokens[newHash].FamilyId);
        }

        [TestMethod]
        public async Task RefreshAsync_WithValidToken_ShouldPreserveClaimsAndRoles()
        {
            List<Claim> claims = new List<Claim> { new Claim("tenant_id", "tenant-42") };
            List<string> roles = new List<string> { "admin", "user" };

            string? serializedClaims = RefreshTokenService.SerializeClaims(claims);
            string? serializedRoles = RefreshTokenService.SerializeRoles(roles);

            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", serializedClaims, serializedRoles);

            RefreshResult result = await _service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsTrue(result.Success);

            // The new stored token should preserve the serialized claims and roles
            string newHash = RefreshTokenService.HashToken(result.NewRefreshToken!);
            StoredRefreshToken newStored = _store.Tokens[newHash];
            Assert.AreEqual(serializedClaims, newStored.SerializedClaims);
            Assert.AreEqual(serializedRoles, newStored.SerializedRoles);
        }

        [TestMethod]
        public async Task RefreshAsync_WithNonexistentToken_ShouldReturnTokenNotFound()
        {
            RefreshResult result = await _service.RefreshAsync("nonexistent-token", _jwtTokenService);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RefreshFailureReason.TokenNotFound, result.FailureReason);
        }

        [TestMethod]
        public async Task RefreshAsync_WithExpiredToken_ShouldReturnTokenExpired()
        {
            RefreshTokenService shortLivedService = new RefreshTokenService(_store, refreshTokenLifetime: TimeSpan.FromMilliseconds(1));

            string rawToken = await shortLivedService.CreateRefreshTokenAsync("user-1", "user", null, null);

            // Wait for the token to expire
            await Task.Delay(50);

            RefreshResult result = await shortLivedService.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RefreshFailureReason.TokenExpired, result.FailureReason);
        }

        [TestMethod]
        public async Task RefreshAsync_WithInvalidatedToken_ShouldReturnTokenInvalidated()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);

            // Manually invalidate the family
            await _store.InvalidateFamilyAsync(_store.Tokens[hash].FamilyId);

            RefreshResult result = await _service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RefreshFailureReason.TokenInvalidated, result.FailureReason);
        }

        [TestMethod]
        public async Task RefreshAsync_WithConsumedToken_ShouldDetectTheftAndInvalidateFamily()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);
            string familyId = _store.Tokens[hash].FamilyId;

            // First refresh — should succeed
            RefreshResult firstResult = await _service.RefreshAsync(rawToken, _jwtTokenService);
            Assert.IsTrue(firstResult.Success);

            // Second refresh with the same (now consumed) token — should detect theft
            RefreshResult secondResult = await _service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsFalse(secondResult.Success);
            Assert.AreEqual(RefreshFailureReason.TheftDetected, secondResult.FailureReason);

            // Verify the entire family is invalidated
            foreach (StoredRefreshToken token in _store.Tokens.Values)
            {
                if (token.FamilyId == familyId)
                {
                    Assert.IsTrue(token.IsInvalidated);
                }
            }
        }

        [TestMethod]
        public async Task RefreshAsync_FullRotationChain_ShouldWork()
        {
            string currentToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);

            // Rotate multiple times
            for (int i = 0; i < 5; i++)
            {
                RefreshResult result = await _service.RefreshAsync(currentToken, _jwtTokenService);
                Assert.IsTrue(result.Success, $"Rotation {i + 1} failed");
                Assert.IsNotNull(result.NewRefreshToken);
                currentToken = result.NewRefreshToken;
            }

            // All tokens should be in the same family
            string firstHash = _store.Tokens.Values.First().FamilyId;
            foreach (StoredRefreshToken token in _store.Tokens.Values)
            {
                Assert.AreEqual(firstHash, token.FamilyId);
            }
        }

        [TestMethod]
        public void HashToken_ShouldReturnConsistentResults()
        {
            string token = "test-token-value";

            string hash1 = RefreshTokenService.HashToken(token);
            string hash2 = RefreshTokenService.HashToken(token);

            Assert.AreEqual(hash1, hash2);
            Assert.AreEqual(64, hash1.Length); // SHA-256 produces 32 bytes = 64 hex chars
        }

        [TestMethod]
        public void SerializeClaims_WithClaims_ShouldRoundTrip()
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim("tenant_id", "tenant-42"),
                new Claim("email", "test@example.com")
            };

            string? serialized = RefreshTokenService.SerializeClaims(claims);

            Assert.IsNotNull(serialized);
            Assert.IsTrue(serialized.Contains("tenant_id"));
            Assert.IsTrue(serialized.Contains("tenant-42"));
        }

        [TestMethod]
        public void SerializeClaims_WithEmptyClaims_ShouldReturnNull()
        {
            string? serialized = RefreshTokenService.SerializeClaims(new List<Claim>());
            Assert.IsNull(serialized);
        }

        [TestMethod]
        public void SerializeRoles_WithRoles_ShouldRoundTrip()
        {
            List<string> roles = new List<string> { "admin", "user" };

            string? serialized = RefreshTokenService.SerializeRoles(roles);

            Assert.IsNotNull(serialized);
            Assert.IsTrue(serialized.Contains("admin"));
            Assert.IsTrue(serialized.Contains("user"));
        }

        [TestMethod]
        public void SerializeRoles_WithEmptyRoles_ShouldReturnNull()
        {
            string? serialized = RefreshTokenService.SerializeRoles(new List<string>());
            Assert.IsNull(serialized);
        }
    }
}
