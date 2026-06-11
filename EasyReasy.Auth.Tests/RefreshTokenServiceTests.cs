using System.IdentityModel.Tokens.Jwt;
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

        private static IEnumerable<Claim> DecodeClaims(string accessToken)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims;
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
        public async Task CreateRefreshTokenAsync_WithAllowMultiplePolicy_ShouldLeavePriorFamiliesValid()
        {
            // The default service (_service) uses ConcurrentSessionPolicy.AllowMultiple.
            string firstToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            await _service.CreateRefreshTokenAsync("user-1", "user", null, null);

            string firstHash = RefreshTokenService.HashToken(firstToken);
            Assert.IsFalse(_store.Tokens[firstHash].IsInvalidated);
            Assert.AreEqual(2, _store.Tokens.Count, "both sessions should coexist under AllowMultiple");
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_WithSingleSessionPolicy_ShouldInvalidatePriorFamiliesForSameSubject()
        {
            RefreshTokenService service = new RefreshTokenService(_store, concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);

            string firstToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string secondToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            string firstHash = RefreshTokenService.HashToken(firstToken);
            string secondHash = RefreshTokenService.HashToken(secondToken);

            Assert.IsTrue(_store.Tokens[firstHash].IsInvalidated, "The earlier session should have been revoked.");
            Assert.IsFalse(_store.Tokens[secondHash].IsInvalidated, "The newest session must survive its own enforcement.");
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_WithSingleSessionPolicy_ShouldKeepOnlyNewestSessionRefreshable()
        {
            RefreshTokenService service = new RefreshTokenService(_store, concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);

            string firstToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string secondToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            RefreshResult oldResult = await service.RefreshAsync(firstToken, _jwtTokenService);
            RefreshResult newResult = await service.RefreshAsync(secondToken, _jwtTokenService);

            Assert.IsFalse(oldResult.Success);
            Assert.AreEqual(RefreshFailureReason.TokenInvalidated, oldResult.FailureReason);
            Assert.IsTrue(newResult.Success);
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_WithSingleSessionPolicy_ShouldNotInvalidateOtherSubjects()
        {
            RefreshTokenService service = new RefreshTokenService(_store, concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);

            string otherUserToken = await service.CreateRefreshTokenAsync("user-2", "user", null, null);
            await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            string otherHash = RefreshTokenService.HashToken(otherUserToken);
            Assert.IsFalse(_store.Tokens[otherHash].IsInvalidated);
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_WithSingleSessionPolicy_AndPriorSessions_ShouldFireConcurrentSessionsRevokedAudit()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = new RefreshTokenService(_store, auditLogger: auditLogger, concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);

            await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            Assert.AreEqual(1, auditLogger.ConcurrentSessionsRevokedCalls.Count);
            Assert.AreEqual("user-1", auditLogger.ConcurrentSessionsRevokedCalls[0].Subject);
            Assert.AreEqual(1, auditLogger.ConcurrentSessionsRevokedCalls[0].InvalidatedFamilyCount);
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_WithSingleSessionPolicy_AndNoPriorSessions_ShouldNotFireConcurrentSessionsRevokedAudit()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = new RefreshTokenService(_store, auditLogger: auditLogger, concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);

            await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            Assert.AreEqual(0, auditLogger.ConcurrentSessionsRevokedCalls.Count);
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

            string? serializedClaims = RefreshTokenClaims.SerializeClaims(claims);
            string? serializedRoles = RefreshTokenClaims.SerializeRoles(roles);

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
        public async Task LogoutAsync_WithValidToken_ShouldInvalidateFamily()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);
            string familyId = _store.Tokens[hash].FamilyId;

            await _service.LogoutAsync(rawToken);

            Assert.IsTrue(_store.Tokens[hash].IsInvalidated);
            Assert.AreEqual(familyId, _store.Tokens[hash].FamilyId);
        }

        [TestMethod]
        public async Task LogoutAsync_WithNullToken_ShouldCompleteSilently()
        {
            await _service.LogoutAsync(null);

            Assert.AreEqual(0, _store.Tokens.Count);
        }

        [TestMethod]
        public async Task LogoutAsync_WithEmptyToken_ShouldCompleteSilently()
        {
            await _service.LogoutAsync(string.Empty);

            Assert.AreEqual(0, _store.Tokens.Count);
        }

        [TestMethod]
        public async Task LogoutAsync_WithNonexistentToken_ShouldCompleteSilently()
        {
            await _service.LogoutAsync("nonexistent-token");

            Assert.AreEqual(0, _store.Tokens.Count);
        }

        [TestMethod]
        public async Task LogoutAsync_CalledTwice_ShouldCompleteSilently()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);

            await _service.LogoutAsync(rawToken);
            await _service.LogoutAsync(rawToken);

            string hash = RefreshTokenService.HashToken(rawToken);
            Assert.IsTrue(_store.Tokens[hash].IsInvalidated);
        }

        [TestMethod]
        public async Task LogoutAsync_AfterRotations_ShouldInvalidateEntireFamily()
        {
            string currentToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string originalHash = RefreshTokenService.HashToken(currentToken);
            string familyId = _store.Tokens[originalHash].FamilyId;

            // Rotate a couple of times
            for (int i = 0; i < 3; i++)
            {
                RefreshResult result = await _service.RefreshAsync(currentToken, _jwtTokenService);
                Assert.IsTrue(result.Success);
                currentToken = result.NewRefreshToken!;
            }

            await _service.LogoutAsync(currentToken);

            // Every token in the family should now be invalidated
            foreach (StoredRefreshToken token in _store.Tokens.Values)
            {
                if (token.FamilyId == familyId)
                {
                    Assert.IsTrue(token.IsInvalidated);
                }
            }
        }

        [TestMethod]
        public async Task LogoutAsync_WithConsumedButNotInvalidatedToken_ShouldInvalidateFamily()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string originalHash = RefreshTokenService.HashToken(rawToken);
            string familyId = _store.Tokens[originalHash].FamilyId;

            // Rotate once — the original token is now consumed but the family is not invalidated
            RefreshResult refreshResult = await _service.RefreshAsync(rawToken, _jwtTokenService);
            Assert.IsTrue(refreshResult.Success);
            Assert.IsNotNull(_store.Tokens[originalHash].ConsumedAt);
            Assert.IsFalse(_store.Tokens[originalHash].IsInvalidated);

            // Logging out with the consumed (but not invalidated) token must invalidate the family
            await _service.LogoutAsync(rawToken);

            foreach (StoredRefreshToken token in _store.Tokens.Values)
            {
                if (token.FamilyId == familyId)
                {
                    Assert.IsTrue(token.IsInvalidated);
                }
            }
        }

        [TestMethod]
        public async Task LogoutAsync_OnlyAffectsTargetedFamily()
        {
            string userOneToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string userTwoToken = await _service.CreateRefreshTokenAsync("user-2", "user", null, null);

            await _service.LogoutAsync(userOneToken);

            string userTwoHash = RefreshTokenService.HashToken(userTwoToken);
            Assert.IsFalse(_store.Tokens[userTwoHash].IsInvalidated);
        }

        [TestMethod]
        public async Task InvalidateAllSessionsAsync_WithMultipleFamiliesForUser_ShouldInvalidateAll()
        {
            string userOneTokenA = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string userOneTokenB = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string userOneTokenC = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string userTwoToken = await _service.CreateRefreshTokenAsync("user-2", "user", null, null);

            await _service.InvalidateAllSessionsAsync("user-1");

            Assert.IsTrue(_store.Tokens[RefreshTokenService.HashToken(userOneTokenA)].IsInvalidated);
            Assert.IsTrue(_store.Tokens[RefreshTokenService.HashToken(userOneTokenB)].IsInvalidated);
            Assert.IsTrue(_store.Tokens[RefreshTokenService.HashToken(userOneTokenC)].IsInvalidated);
            Assert.IsFalse(_store.Tokens[RefreshTokenService.HashToken(userTwoToken)].IsInvalidated);
        }

        [TestMethod]
        public async Task InvalidateAllSessionsAsync_WithNoSessions_ShouldNotMutateStore()
        {
            await _service.InvalidateAllSessionsAsync("user-with-no-tokens");

            Assert.AreEqual(0, _store.Tokens.Count);
        }

        [TestMethod]
        public async Task InvalidateAllSessionsAsync_AfterRotations_ShouldInvalidateAllFamilyMembers()
        {
            string currentToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);

            for (int i = 0; i < 3; i++)
            {
                RefreshResult result = await _service.RefreshAsync(currentToken, _jwtTokenService);
                currentToken = result.NewRefreshToken!;
            }

            SessionRevocationResult revocation = await _service.InvalidateAllSessionsAsync("user-1");

            foreach (StoredRefreshToken token in _store.Tokens.Values)
            {
                Assert.IsTrue(token.IsInvalidated);
            }

            // Three rotations put four tokens into one family, so the family count should be 1
            // even though four individual token rows are invalidated.
            Assert.AreEqual(1, revocation.InvalidatedFamilyCount);
        }

        [TestMethod]
        public async Task RefreshAsync_WithValidToken_ShouldPopulateSubjectAndFamilyIdOnResult()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);
            string expectedFamilyId = _store.Tokens[hash].FamilyId;

            RefreshResult result = await _service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("user-1", result.Subject);
            Assert.AreEqual(expectedFamilyId, result.FamilyId);
        }

        [TestMethod]
        public async Task RefreshAsync_WithNonexistentToken_ShouldReturnNullSubjectAndFamilyId()
        {
            RefreshResult result = await _service.RefreshAsync("nonexistent-token", _jwtTokenService);

            Assert.AreEqual(RefreshFailureReason.TokenNotFound, result.FailureReason);
            Assert.IsNull(result.Subject);
            Assert.IsNull(result.FamilyId);
        }

        [TestMethod]
        public async Task RefreshAsync_WithInvalidatedToken_ShouldPopulateSubjectAndFamilyIdOnResult()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);
            string expectedFamilyId = _store.Tokens[hash].FamilyId;

            await _store.InvalidateFamilyAsync(expectedFamilyId);

            RefreshResult result = await _service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.AreEqual(RefreshFailureReason.TokenInvalidated, result.FailureReason);
            Assert.AreEqual("user-1", result.Subject);
            Assert.AreEqual(expectedFamilyId, result.FamilyId);
        }

        [TestMethod]
        public async Task RefreshAsync_WithExpiredToken_ShouldPopulateSubjectAndFamilyIdOnResult()
        {
            RefreshTokenService shortLivedService = new RefreshTokenService(_store, refreshTokenLifetime: TimeSpan.FromMilliseconds(1));
            string rawToken = await shortLivedService.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);
            string expectedFamilyId = _store.Tokens[hash].FamilyId;

            await Task.Delay(50);

            RefreshResult result = await shortLivedService.RefreshAsync(rawToken, _jwtTokenService);

            Assert.AreEqual(RefreshFailureReason.TokenExpired, result.FailureReason);
            Assert.AreEqual("user-1", result.Subject);
            Assert.AreEqual(expectedFamilyId, result.FamilyId);
        }

        [TestMethod]
        public async Task RefreshAsync_WithConsumedToken_ShouldPopulateSubjectAndFamilyIdOnTheftResult()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);
            string expectedFamilyId = _store.Tokens[hash].FamilyId;

            await _service.RefreshAsync(rawToken, _jwtTokenService);

            RefreshResult replayResult = await _service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.AreEqual(RefreshFailureReason.TheftDetected, replayResult.FailureReason);
            Assert.AreEqual("user-1", replayResult.Subject);
            Assert.AreEqual(expectedFamilyId, replayResult.FamilyId);
        }

        [TestMethod]
        public async Task LogoutAsync_WithKnownToken_ShouldReturnKnownResultWithSubjectAndFamilyId()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);
            string expectedFamilyId = _store.Tokens[hash].FamilyId;

            LogoutResult result = await _service.LogoutAsync(rawToken);

            Assert.IsTrue(result.WasKnown);
            Assert.AreEqual("user-1", result.Subject);
            Assert.AreEqual(expectedFamilyId, result.FamilyId);
        }

        [TestMethod]
        public async Task LogoutAsync_WithNullToken_ShouldReturnUnknownResult()
        {
            LogoutResult result = await _service.LogoutAsync(null);

            Assert.IsFalse(result.WasKnown);
            Assert.IsNull(result.Subject);
            Assert.IsNull(result.FamilyId);
        }

        [TestMethod]
        public async Task LogoutAsync_WithEmptyToken_ShouldReturnUnknownResult()
        {
            LogoutResult result = await _service.LogoutAsync(string.Empty);

            Assert.IsFalse(result.WasKnown);
        }

        [TestMethod]
        public async Task LogoutAsync_WithNonexistentToken_ShouldReturnUnknownResult()
        {
            LogoutResult result = await _service.LogoutAsync("nonexistent-token");

            Assert.IsFalse(result.WasKnown);
        }

        [TestMethod]
        public async Task InvalidateAllSessionsAsync_ShouldReturnSubjectAndCountOfInvalidatedFamilies()
        {
            await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            await _service.CreateRefreshTokenAsync("user-2", "user", null, null);

            SessionRevocationResult result = await _service.InvalidateAllSessionsAsync("user-1");

            Assert.AreEqual("user-1", result.Subject);
            Assert.AreEqual(3, result.InvalidatedFamilyCount);
        }

        [TestMethod]
        public async Task InvalidateAllSessionsAsync_WithNoSessions_ShouldReturnZeroCount()
        {
            SessionRevocationResult result = await _service.InvalidateAllSessionsAsync("user-with-no-tokens");

            Assert.AreEqual("user-with-no-tokens", result.Subject);
            Assert.AreEqual(0, result.InvalidatedFamilyCount);
        }

        [TestMethod]
        public async Task InvalidateAllSessionsAsync_AfterRotations_ShouldCountFamiliesNotIndividualTokens()
        {
            string currentToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            for (int i = 0; i < 3; i++)
            {
                RefreshResult r = await _service.RefreshAsync(currentToken, _jwtTokenService);
                currentToken = r.NewRefreshToken!;
            }

            SessionRevocationResult result = await _service.InvalidateAllSessionsAsync("user-1");

            Assert.AreEqual(1, result.InvalidatedFamilyCount);
        }

        [TestMethod]
        public async Task InvalidateAllSessionsAsync_WithAuditLogger_ShouldInvokeOnSessionsInvalidatedAsync()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService serviceWithAuditor = new RefreshTokenService(_store, auditLogger: auditLogger);

            await serviceWithAuditor.CreateRefreshTokenAsync("user-1", "user", null, null);
            await serviceWithAuditor.CreateRefreshTokenAsync("user-1", "user", null, null);

            SessionRevocationResult result = await serviceWithAuditor.InvalidateAllSessionsAsync("user-1");

            Assert.AreEqual(1, auditLogger.SessionsInvalidatedCalls.Count);
            Assert.AreSame(result, auditLogger.SessionsInvalidatedCalls[0]);
        }

        [TestMethod]
        public async Task LogoutAsync_ProgrammaticCall_ShouldInvokeOnLogoutAsyncWithNullHttpContext()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService serviceWithAuditor = new RefreshTokenService(_store, auditLogger: auditLogger);

            string rawToken = await serviceWithAuditor.CreateRefreshTokenAsync("user-1", "user", null, null);

            LogoutResult result = await serviceWithAuditor.LogoutAsync(rawToken);

            Assert.AreEqual(1, auditLogger.LogoutCalls.Count);
            Assert.IsNull(auditLogger.LogoutCalls[0].HttpContext);
            Assert.AreSame(result, auditLogger.LogoutCalls[0].Result);
            Assert.IsTrue(result.WasKnown);
        }

        [TestMethod]
        public async Task LogoutAsync_WithUnknownToken_ShouldStillInvokeOnLogoutAsync()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService serviceWithAuditor = new RefreshTokenService(_store, auditLogger: auditLogger);

            LogoutResult result = await serviceWithAuditor.LogoutAsync("nonexistent-token");

            Assert.AreEqual(1, auditLogger.LogoutCalls.Count);
            Assert.IsFalse(result.WasKnown);
            Assert.IsFalse(auditLogger.LogoutCalls[0].Result.WasKnown);
        }

        [TestMethod]
        public async Task CreateRefreshTokenWithFamilyAsync_ReturnsFamilyIdMatchingStoredToken()
        {
            RefreshTokenCreationResult result = await _service.CreateRefreshTokenWithFamilyAsync("user-1", "user", null, null);

            string hash = RefreshTokenService.HashToken(result.RawToken);
            Assert.IsTrue(_store.Tokens.ContainsKey(hash), "the returned raw token should hash to a stored entry");
            Assert.AreEqual(_store.Tokens[hash].FamilyId, result.FamilyId);
        }

        [TestMethod]
        public async Task CreateRefreshTokenAsync_ReturnsRawTokenFromSharedCreationPath()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);

            // The store is keyed by HashToken(rawToken), so a hit proves the legacy method returned the raw
            // token itself (not the hash or the family id, neither of which would hash to this key).
            string hash = RefreshTokenService.HashToken(rawToken);
            Assert.IsTrue(_store.Tokens.ContainsKey(hash), "the legacy method must return the raw token");
        }

        [TestMethod]
        public async Task RefreshAsync_AfterRefresh_AccessTokenCarriesSingleFamilyIdClaimEqualToFamilyId()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string familyId = _store.Tokens[RefreshTokenService.HashToken(rawToken)].FamilyId;

            RefreshResult result = await _service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsTrue(result.Success);
            List<Claim> issued = DecodeClaims(result.AuthResponse!.Token).ToList();
            Assert.AreEqual(1, issued.Count(c => c.Type == "family_id"));
            Assert.AreEqual(familyId, issued.Single(c => c.Type == "family_id").Value);
        }

        [TestMethod]
        public async Task RefreshAsync_AcrossRotation_FamilyIdClaimUnchanged()
        {
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string familyId = _store.Tokens[RefreshTokenService.HashToken(rawToken)].FamilyId;

            RefreshResult first = await _service.RefreshAsync(rawToken, _jwtTokenService);
            RefreshResult second = await _service.RefreshAsync(first.NewRefreshToken!, _jwtTokenService);

            string firstFamily = DecodeClaims(first.AuthResponse!.Token).Single(c => c.Type == "family_id").Value;
            string secondFamily = DecodeClaims(second.AuthResponse!.Token).Single(c => c.Type == "family_id").Value;
            Assert.AreEqual(familyId, firstFamily);
            Assert.AreEqual(familyId, secondFamily);
        }

        [TestMethod]
        public async Task RefreshAsync_WithSpoofedFamilyIdInStoredClaims_OverwritesWithAuthoritativeValue()
        {
            string? spoofedClaims = RefreshTokenClaims.SerializeClaims(new List<Claim> { new Claim("family_id", "attacker-supplied") });
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", spoofedClaims, null);
            string familyId = _store.Tokens[RefreshTokenService.HashToken(rawToken)].FamilyId;

            RefreshResult result = await _service.RefreshAsync(rawToken, _jwtTokenService);

            List<Claim> issued = DecodeClaims(result.AuthResponse!.Token).ToList();
            Assert.AreEqual(1, issued.Count(c => c.Type == "family_id"), "the spoofed claim must not be duplicated");
            Assert.AreEqual(familyId, issued.Single(c => c.Type == "family_id").Value);
            Assert.AreEqual(0, issued.Count(c => c.Type == "family_id" && c.Value == "attacker-supplied"));
        }

        [TestMethod]
        public async Task RetireFamilyAsync_WithFamilyId_InvalidatesThatFamilyOnceAndFiresSupersededNotLogout()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = new RefreshTokenService(_store, auditLogger: auditLogger);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string familyId = _store.Tokens[RefreshTokenService.HashToken(rawToken)].FamilyId;

            await service.RetireFamilyAsync(familyId, "user-1");

            Assert.IsTrue(_store.Tokens[RefreshTokenService.HashToken(rawToken)].IsInvalidated);
            Assert.AreEqual(1, _store.InvalidatedFamilyIds.Count(id => id == familyId), "InvalidateFamilyAsync should be called exactly once for the family");
            Assert.AreEqual(0, auditLogger.LogoutCalls.Count, "a supersession must not be recorded as a logout");
            Assert.AreEqual(1, auditLogger.SessionSupersededCalls.Count);
            Assert.AreEqual(familyId, auditLogger.SessionSupersededCalls[0].Result.FamilyId);
            Assert.AreEqual("user-1", auditLogger.SessionSupersededCalls[0].Result.Subject);
        }

        [TestMethod]
        public async Task RetireFamilyAsync_LeavesOtherFamiliesLive()
        {
            string priorToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string otherDeviceToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string otherUserToken = await _service.CreateRefreshTokenAsync("user-2", "user", null, null);

            string priorFamilyId = _store.Tokens[RefreshTokenService.HashToken(priorToken)].FamilyId;

            await _service.RetireFamilyAsync(priorFamilyId, "user-1");

            Assert.IsTrue(_store.Tokens[RefreshTokenService.HashToken(priorToken)].IsInvalidated);
            Assert.IsFalse(_store.Tokens[RefreshTokenService.HashToken(otherDeviceToken)].IsInvalidated, "the subject's other device must stay live");
            Assert.IsFalse(_store.Tokens[RefreshTokenService.HashToken(otherUserToken)].IsInvalidated, "another subject must stay live");
        }

        [TestMethod]
        public async Task RetireFamilyAsync_WithoutAuditLogger_StillInvalidatesFamily()
        {
            // The default _service has no audit logger — the hook block must be skipped, not throw.
            string rawToken = await _service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string familyId = _store.Tokens[RefreshTokenService.HashToken(rawToken)].FamilyId;

            await _service.RetireFamilyAsync(familyId, "user-1");

            Assert.IsTrue(_store.Tokens[RefreshTokenService.HashToken(rawToken)].IsInvalidated);
        }

        [TestMethod]
        public async Task RetireFamilyAsync_WithNonExistentFamilyId_StillCallsStoreAndFiresSuperseded()
        {
            // A non-empty family id always acts: the store can't report whether the family existed, so the
            // contract is "a retire was requested for this family" — InvalidateFamilyAsync is called and the
            // hook fires even when nothing matches, without throwing.
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = new RefreshTokenService(_store, auditLogger: auditLogger);

            await service.RetireFamilyAsync("no-such-family", "user-1");

            Assert.AreEqual(1, _store.InvalidatedFamilyIds.Count(id => id == "no-such-family"));
            Assert.AreEqual(1, auditLogger.SessionSupersededCalls.Count);
            Assert.AreEqual("no-such-family", auditLogger.SessionSupersededCalls[0].Result.FamilyId);
            Assert.AreEqual("user-1", auditLogger.SessionSupersededCalls[0].Result.Subject);
        }

        [TestMethod]
        public async Task RetireFamilyAsync_WithNullFamilyId_IsNoOp()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = new RefreshTokenService(_store, auditLogger: auditLogger);
            await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            await service.RetireFamilyAsync(null, "user-1");

            Assert.AreEqual(0, _store.InvalidatedFamilyIds.Count);
            Assert.AreEqual(0, auditLogger.SessionSupersededCalls.Count);
        }

        [TestMethod]
        public async Task RetireFamilyAsync_WithEmptyFamilyId_IsNoOp()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = new RefreshTokenService(_store, auditLogger: auditLogger);

            await service.RetireFamilyAsync(string.Empty);

            Assert.AreEqual(0, _store.InvalidatedFamilyIds.Count);
            Assert.AreEqual(0, auditLogger.SessionSupersededCalls.Count);
        }

        [TestMethod]
        public async Task RetireFamilyAsync_WithWhitespaceFamilyId_IsNoOp()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = new RefreshTokenService(_store, auditLogger: auditLogger);

            await service.RetireFamilyAsync("   ");

            Assert.AreEqual(0, _store.InvalidatedFamilyIds.Count);
            Assert.AreEqual(0, auditLogger.SessionSupersededCalls.Count);
        }
    }
}
