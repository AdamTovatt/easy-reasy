using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class RefreshTokenServiceClaimsResolverTests
    {
        private const string TestSecret = "super_secret_key_12345_12345_12345";
        private const string TestIssuer = "test-issuer";

        private FakeRefreshTokenStore _store = null!;
        private IJwtTokenService _jwtTokenService = null!;

        [TestInitialize]
        public void Setup()
        {
            _store = new FakeRefreshTokenStore();
            _jwtTokenService = new JwtTokenService(TestSecret, TestIssuer);
        }

        private RefreshTokenService BuildService(
            IRefreshClaimsResolver? resolver = null,
            IAuthAuditLogger? auditLogger = null)
        {
            return new RefreshTokenService(_store, auditLogger: auditLogger, claimsResolver: resolver);
        }

        private static FakeRefreshClaimsResolver AllowReturning(IEnumerable<Claim> claims, IEnumerable<string> roles)
        {
            return new FakeRefreshClaimsResolver(_ => RefreshClaimsDecision.Allow(claims, roles));
        }

        private static FakeRefreshClaimsResolver Denying()
        {
            return new FakeRefreshClaimsResolver(_ => RefreshClaimsDecision.Deny());
        }

        private static FakeRefreshClaimsResolver Throwing(Exception exception)
        {
            return new FakeRefreshClaimsResolver(_ => throw exception);
        }

        private static IEnumerable<Claim> DecodeClaims(string accessToken)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims;
        }

        [TestMethod]
        public async Task RefreshAsync_WithNoResolver_ShouldPreserveStoredSerializedClaimsAndRolesOnNewRow()
        {
            List<Claim> claims = new List<Claim> { new Claim("tenant_id", "tenant-42") };
            List<string> roles = new List<string> { "admin" };
            string? serializedClaims = RefreshTokenService.SerializeClaims(claims);
            string? serializedRoles = RefreshTokenService.SerializeRoles(roles);

            RefreshTokenService service = BuildService();
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", serializedClaims, serializedRoles);

            RefreshResult result = await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsTrue(result.Success);
            StoredRefreshToken newStored = _store.Tokens[RefreshTokenService.HashToken(result.NewRefreshToken!)];
            Assert.AreEqual(serializedClaims, newStored.SerializedClaims);
            Assert.AreEqual(serializedRoles, newStored.SerializedRoles);
        }

        [TestMethod]
        public async Task RefreshAsync_WithNoResolver_ShouldPlaceStoredClaimsAndRolesOnAccessToken()
        {
            List<Claim> claims = new List<Claim> { new Claim("tenant_id", "tenant-42") };
            List<string> roles = new List<string> { "admin" };
            string? serializedClaims = RefreshTokenService.SerializeClaims(claims);
            string? serializedRoles = RefreshTokenService.SerializeRoles(roles);

            RefreshTokenService service = BuildService();
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", serializedClaims, serializedRoles);

            RefreshResult result = await service.RefreshAsync(rawToken, _jwtTokenService);

            IEnumerable<Claim> issued = DecodeClaims(result.AuthResponse!.Token);
            Assert.AreEqual(1, issued.Count(c => c.Type == "tenant_id" && c.Value == "tenant-42"));
            Assert.AreEqual(1, issued.Count(c => c.Type == ClaimTypes.Role && c.Value == "admin"));
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverReturningClaims_ShouldUseResolvedClaimsOnAccessToken()
        {
            FakeRefreshClaimsResolver resolver = AllowReturning(
                new List<Claim> { new Claim("tenant_id", "tenant-99") },
                Array.Empty<string>());
            RefreshTokenService service = BuildService(resolver);

            string? storedClaims = RefreshTokenService.SerializeClaims(new List<Claim> { new Claim("tenant_id", "tenant-42") });
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", storedClaims, null);

            RefreshResult result = await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsTrue(result.Success);
            IEnumerable<Claim> issued = DecodeClaims(result.AuthResponse!.Token);
            Assert.AreEqual(1, issued.Count(c => c.Type == "tenant_id" && c.Value == "tenant-99"));
            Assert.AreEqual(0, issued.Count(c => c.Type == "tenant_id" && c.Value == "tenant-42"));
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverReturningClaims_ShouldPersistResolvedClaimsForNextRotation()
        {
            // First refresh runs under a resolver that swaps tenant-42 for tenant-99. Then we
            // drop the resolver and refresh again; the access token from that second hop must
            // still carry tenant-99 — proving the resolved claims survived a full rotation,
            // not just landed on the first issued JWT.
            FakeRefreshClaimsResolver resolver = AllowReturning(
                new List<Claim> { new Claim("tenant_id", "tenant-99") },
                Array.Empty<string>());
            RefreshTokenService serviceWithResolver = BuildService(resolver);
            RefreshTokenService serviceWithoutResolver = BuildService();

            string? storedClaims = RefreshTokenService.SerializeClaims(new List<Claim> { new Claim("tenant_id", "tenant-42") });
            string firstToken = await serviceWithResolver.CreateRefreshTokenAsync("user-1", "user", storedClaims, null);

            RefreshResult firstRefresh = await serviceWithResolver.RefreshAsync(firstToken, _jwtTokenService);
            RefreshResult secondRefresh = await serviceWithoutResolver.RefreshAsync(firstRefresh.NewRefreshToken!, _jwtTokenService);

            Assert.IsTrue(secondRefresh.Success);
            IEnumerable<Claim> issued = DecodeClaims(secondRefresh.AuthResponse!.Token);
            Assert.AreEqual(1, issued.Count(c => c.Type == "tenant_id" && c.Value == "tenant-99"));
            Assert.AreEqual(0, issued.Count(c => c.Type == "tenant_id" && c.Value == "tenant-42"));
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverReturningRoles_ShouldUseResolvedRolesOnAccessToken()
        {
            FakeRefreshClaimsResolver resolver = AllowReturning(
                Array.Empty<Claim>(),
                new List<string> { "user" });
            RefreshTokenService service = BuildService(resolver);

            string? storedRoles = RefreshTokenService.SerializeRoles(new List<string> { "admin" });
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, storedRoles);

            RefreshResult result = await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsTrue(result.Success);
            IEnumerable<Claim> issued = DecodeClaims(result.AuthResponse!.Token);
            Assert.AreEqual(1, issued.Count(c => c.Type == ClaimTypes.Role && c.Value == "user"));
            Assert.AreEqual(0, issued.Count(c => c.Type == ClaimTypes.Role && c.Value == "admin"));
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverAllowingEmpty_ShouldNullOutStoredSerializedFields()
        {
            // Stored row had tenant_id + role. Resolver returns Allow(empty, empty). The new
            // stored row must reflect the resolver's "no claims, no roles" verdict — i.e. both
            // serialized fields become null, NOT the original JSON. Pins the "Allow erases"
            // semantic that subtly differs from the no-resolver pass-through path.
            FakeRefreshClaimsResolver resolver = AllowReturning(Array.Empty<Claim>(), Array.Empty<string>());
            RefreshTokenService service = BuildService(resolver);

            string? storedClaims = RefreshTokenService.SerializeClaims(new List<Claim> { new Claim("tenant_id", "tenant-42") });
            string? storedRoles = RefreshTokenService.SerializeRoles(new List<string> { "admin" });
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", storedClaims, storedRoles);

            RefreshResult result = await service.RefreshAsync(rawToken, _jwtTokenService);

            StoredRefreshToken newStored = _store.Tokens[RefreshTokenService.HashToken(result.NewRefreshToken!)];
            Assert.IsNull(newStored.SerializedClaims);
            Assert.IsNull(newStored.SerializedRoles);
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverDenying_ShouldReturnDeniedByResolverFailure()
        {
            FakeRefreshClaimsResolver resolver = Denying();
            RefreshTokenService service = BuildService(resolver);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string expectedFamilyId = _store.Tokens[RefreshTokenService.HashToken(rawToken)].FamilyId;

            RefreshResult result = await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RefreshFailureReason.DeniedByResolver, result.FailureReason);
            Assert.AreEqual("user-1", result.Subject);
            Assert.AreEqual(expectedFamilyId, result.FamilyId);
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverDenying_ShouldNotConsumeStoredToken()
        {
            FakeRefreshClaimsResolver resolver = Denying();
            RefreshTokenService service = BuildService(resolver);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);

            await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsNull(_store.Tokens[hash].ConsumedAt);
            Assert.IsFalse(_store.Tokens[hash].IsInvalidated);
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverThrowing_ShouldPropagateException()
        {
            InvalidOperationException injected = new InvalidOperationException("resolver blew up");
            FakeRefreshClaimsResolver resolver = Throwing(injected);
            RefreshTokenService service = BuildService(resolver);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.RefreshAsync(rawToken, _jwtTokenService));
            Assert.AreSame(injected, thrown);
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverThrowing_ShouldNotConsumeStoredToken()
        {
            FakeRefreshClaimsResolver resolver = Throwing(new InvalidOperationException("resolver blew up"));
            RefreshTokenService service = BuildService(resolver);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string hash = RefreshTokenService.HashToken(rawToken);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.RefreshAsync(rawToken, _jwtTokenService));

            Assert.IsNull(_store.Tokens[hash].ConsumedAt);
            Assert.IsFalse(_store.Tokens[hash].IsInvalidated);
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverAllowing_ShouldStillDetectSequentialReuseAsTheft()
        {
            FakeRefreshClaimsResolver resolver = AllowReturning(Array.Empty<Claim>(), Array.Empty<string>());
            RefreshTokenService service = BuildService(resolver);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);
            string familyId = _store.Tokens[RefreshTokenService.HashToken(rawToken)].FamilyId;

            RefreshResult firstResult = await service.RefreshAsync(rawToken, _jwtTokenService);
            Assert.IsTrue(firstResult.Success);

            RefreshResult secondResult = await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsFalse(secondResult.Success);
            Assert.AreEqual(RefreshFailureReason.TheftDetected, secondResult.FailureReason);

            foreach (StoredRefreshToken token in _store.Tokens.Values)
            {
                if (token.FamilyId == familyId)
                {
                    Assert.IsTrue(token.IsInvalidated);
                }
            }
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolver_ShouldPassStoredClaimsAndRolesOnContext()
        {
            FakeRefreshClaimsResolver resolver = AllowReturning(Array.Empty<Claim>(), Array.Empty<string>());
            RefreshTokenService service = BuildService(resolver);

            string? storedClaims = RefreshTokenService.SerializeClaims(new List<Claim>
            {
                new Claim("tenant_id", "tenant-42"),
                new Claim("email", "test@example.com"),
            });
            string? storedRoles = RefreshTokenService.SerializeRoles(new List<string> { "admin", "auditor" });

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", storedClaims, storedRoles);

            await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.AreEqual(1, resolver.Calls.Count);
            RefreshClaimsContext captured = resolver.Calls[0];
            Assert.AreEqual(1, captured.StoredClaims.Count(c => c.Type == "tenant_id" && c.Value == "tenant-42"));
            Assert.AreEqual(1, captured.StoredClaims.Count(c => c.Type == "email" && c.Value == "test@example.com"));
            CollectionAssert.AreEquivalent(new List<string> { "admin", "auditor" }, captured.StoredRoles.ToList());
        }

        [TestMethod]
        public async Task RefreshAsync_WithHttpContextProvided_ShouldPassItToResolver()
        {
            FakeRefreshClaimsResolver resolver = AllowReturning(Array.Empty<Claim>(), Array.Empty<string>());
            RefreshTokenService service = BuildService(resolver);

            HttpContext httpContext = new DefaultHttpContext();
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            RefreshResult result = await service.RefreshAsync(rawToken, _jwtTokenService, httpContext);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, resolver.Calls.Count);
            Assert.AreSame(httpContext, resolver.Calls[0].HttpContext);
            Assert.AreEqual("user-1", resolver.Calls[0].Subject);
            Assert.AreEqual("user", resolver.Calls[0].AuthType);
        }

        [TestMethod]
        public async Task RefreshAsync_WithNullHttpContext_ShouldStillInvokeResolver()
        {
            FakeRefreshClaimsResolver resolver = AllowReturning(Array.Empty<Claim>(), Array.Empty<string>());
            RefreshTokenService service = BuildService(resolver);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            RefreshResult result = await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, resolver.Calls.Count);
            Assert.IsNull(resolver.Calls[0].HttpContext);
        }

        [TestMethod]
        public async Task RefreshAsync_WithCancellationToken_ShouldPropagateItToResolver()
        {
            FakeRefreshClaimsResolver resolver = AllowReturning(Array.Empty<Claim>(), Array.Empty<string>());
            RefreshTokenService service = BuildService(resolver);

            using CancellationTokenSource cts = new CancellationTokenSource();
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            await service.RefreshAsync(rawToken, _jwtTokenService, httpContext: null, cancellationToken: cts.Token);

            Assert.AreEqual(1, resolver.ReceivedCancellationTokens.Count);
            Assert.AreEqual(cts.Token, resolver.ReceivedCancellationTokens[0]);
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverDenying_ShouldInvokeAuditLoggerWithDeniedByResolverResult()
        {
            FakeRefreshClaimsResolver resolver = Denying();
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = BuildService(resolver, auditLogger);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            await service.RefreshAsync(rawToken, _jwtTokenService);

            Assert.AreEqual(1, auditLogger.RefreshCalls.Count);
            Assert.IsFalse(auditLogger.RefreshCalls[0].Result.Success);
            Assert.AreEqual(RefreshFailureReason.DeniedByResolver, auditLogger.RefreshCalls[0].Result.FailureReason);
        }

        [TestMethod]
        public async Task RefreshAsync_WithResolverThrowing_ShouldNotInvokeAuditLogger()
        {
            // Pins the documented contract that resolver throws propagate without burning the
            // stored token AND without surfacing an audit event. If a future refactor wraps the
            // resolver in try/finally to "fix" the audit gap, this test catches it.
            FakeRefreshClaimsResolver resolver = Throwing(new InvalidOperationException("resolver blew up"));
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = BuildService(resolver, auditLogger);

            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", null, null);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => service.RefreshAsync(rawToken, _jwtTokenService));

            Assert.AreEqual(0, auditLogger.RefreshCalls.Count);
        }
    }
}
