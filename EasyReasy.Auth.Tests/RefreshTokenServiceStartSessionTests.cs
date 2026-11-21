using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class RefreshTokenServiceStartSessionTests
    {
        private const string TestSecret = "super_secret_key_12345_12345_12345";
        private const string TestIssuer = "test-issuer";

        private static readonly DateTimeOffset StartTime = new DateTimeOffset(2026, 3, 14, 9, 26, 53, TimeSpan.Zero);
        private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(17);
        private static readonly string[] SignInRoles = ["admin", "auditor"];

        /// <summary>
        /// The claims every token gets from <see cref="JwtTokenService"/> itself and that differ per token, left out
        /// when a first token is compared with a refreshed one.
        /// </summary>
        private static readonly HashSet<string> PerTokenClaimTypes = new HashSet<string>
        {
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Nbf,
            JwtRegisteredClaimNames.Exp,
            JwtRegisteredClaimNames.Iss,
        };

        private readonly FakeRefreshTokenStore _store = new FakeRefreshTokenStore();
        private readonly FakeTimeProvider _clock = new FakeTimeProvider(StartTime);

        private RefreshTokenService CreateService(
            IRefreshClaimsResolver? claimsResolver = null,
            IAuthAuditLogger? auditLogger = null,
            ConcurrentSessionPolicy concurrentSessionPolicy = ConcurrentSessionPolicy.AllowMultiple)
        {
            return new RefreshTokenService(
                _store,
                refreshTokenLifetime: null,
                accessTokenLifetime: AccessTokenLifetime,
                auditLogger: auditLogger,
                claimsResolver: claimsResolver,
                concurrentSessionPolicy: concurrentSessionPolicy,
                timeProvider: _clock);
        }

        private JwtTokenService CreateJwtTokenService()
        {
            return new JwtTokenService(TestSecret, TestIssuer, null, _clock);
        }

        private static List<Claim> SignInClaims()
        {
            return new List<Claim>
            {
                new Claim(EasyReasyClaimTypes.TenantId, "tenant-42"),
                new Claim("active_org", "org-7"),
                new Claim("level", "5", ClaimValueTypes.Integer32),
            };
        }

        private static JwtSecurityToken Decode(string accessToken)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        }

        private static List<string> SessionClaimsOf(string accessToken)
        {
            return Decode(accessToken).Claims
                .Where(claim => !PerTokenClaimTypes.Contains(claim.Type))
                .Select(claim => $"{claim.Type}={claim.Value} ({claim.ValueType})")
                .OrderBy(text => text, StringComparer.Ordinal)
                .ToList();
        }

        private StoredRefreshToken StoredRowOf(string rawRefreshToken)
        {
            return _store.Tokens[SecureToken.Hash(rawRefreshToken)];
        }

        [TestMethod]
        public async Task StartSessionAsync_WithClaimsAndRoles_FirstTokenCarriesClaimsRolesAndRealFamilyId()
        {
            RefreshTokenService service = CreateService();

            SessionStartResult session = await service.StartSessionAsync(CreateJwtTokenService(), "user-1", "user", SignInClaims(), SignInRoles);

            JwtSecurityToken token = Decode(session.AuthResponse.Token);
            Assert.AreEqual("user-1", token.Subject);
            Assert.AreEqual("user", token.Claims.Single(claim => claim.Type == EasyReasyClaimTypes.AuthType).Value);
            Assert.AreEqual("tenant-42", token.Claims.Single(claim => claim.Type == EasyReasyClaimTypes.TenantId).Value);
            Assert.AreEqual("org-7", token.Claims.Single(claim => claim.Type == "active_org").Value);
            Assert.AreEqual("5", token.Claims.Single(claim => claim.Type == "level").Value);
            CollectionAssert.AreEquivalent(SignInRoles, token.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value).ToArray());

            string storedFamilyId = StoredRowOf(session.AuthResponse.RefreshToken!).FamilyId;
            Assert.AreEqual(storedFamilyId, session.FamilyId);
            Assert.AreEqual(storedFamilyId, token.Claims.Single(claim => claim.Type == EasyReasyClaimTypes.RefreshFamilyId).Value);
        }

        [TestMethod]
        public async Task StartSessionAsync_WithCallerSuppliedFamilyId_ReplacesItWithRealFamilyId()
        {
            RefreshTokenService service = CreateService();
            List<Claim> claims = SignInClaims();
            claims.Add(new Claim(EasyReasyClaimTypes.RefreshFamilyId, "caller-supplied"));

            SessionStartResult session = await service.StartSessionAsync(CreateJwtTokenService(), "user-1", "user", claims, SignInRoles);

            List<Claim> familyIdClaims = Decode(session.AuthResponse.Token).Claims
                .Where(claim => claim.Type == EasyReasyClaimTypes.RefreshFamilyId)
                .ToList();
            Assert.AreEqual(1, familyIdClaims.Count, "the supplied family id must be replaced, not joined by the real one");
            Assert.AreEqual(session.FamilyId, familyIdClaims[0].Value);
        }

        [TestMethod]
        public async Task StartSessionAsync_OnTheGivenClock_ExpiresAfterTheConfiguredAccessTokenLifetime()
        {
            RefreshTokenService service = CreateService();

            SessionStartResult session = await service.StartSessionAsync(CreateJwtTokenService(), "user-1", "user", SignInClaims(), SignInRoles);

            DateTime expectedExpiry = StartTime.Add(AccessTokenLifetime).UtcDateTime;
            Assert.AreEqual(expectedExpiry.ToString("O"), session.AuthResponse.ExpiresAt);
            Assert.AreEqual(expectedExpiry, Decode(session.AuthResponse.Token).ValidTo);
        }

        [TestMethod]
        public async Task StartSessionAsync_ThenRefreshWithNoResolver_RefreshedTokenCarriesTheSameClaimsRolesAndFamilyId()
        {
            RefreshTokenService service = CreateService();
            JwtTokenService jwtTokenService = CreateJwtTokenService();
            SessionStartResult session = await service.StartSessionAsync(jwtTokenService, "user-1", "user", SignInClaims(), SignInRoles);

            _clock.Advance(TimeSpan.FromMinutes(5));
            RefreshResult refreshed = await service.RefreshAsync(session.AuthResponse.RefreshToken!, jwtTokenService);

            Assert.IsTrue(refreshed.Success);
            CollectionAssert.AreEqual(SessionClaimsOf(session.AuthResponse.Token), SessionClaimsOf(refreshed.AuthResponse!.Token));
        }

        [TestMethod]
        public async Task StartSessionAsync_WithNoClaimsOrRoles_IssuesOnlyTheLibrarysClaimsAndRefreshes()
        {
            // What every sample passes: nothing is stored, and the first token is built from an empty claim list.
            RefreshTokenService service = CreateService();
            JwtTokenService jwtTokenService = CreateJwtTokenService();

            SessionStartResult session = await service.StartSessionAsync(jwtTokenService, "user-1", "user", [], []);

            CollectionAssert.AreEquivalent(
                new[] { JwtRegisteredClaimNames.Sub, EasyReasyClaimTypes.AuthType, EasyReasyClaimTypes.RefreshFamilyId },
                Decode(session.AuthResponse.Token).Claims.Select(claim => claim.Type).Where(type => !PerTokenClaimTypes.Contains(type)).ToArray());

            StoredRefreshToken stored = StoredRowOf(session.AuthResponse.RefreshToken!);
            Assert.IsNull(stored.SerializedClaims);
            Assert.IsNull(stored.SerializedRoles);

            RefreshResult refreshed = await service.RefreshAsync(session.AuthResponse.RefreshToken!, jwtTokenService);
            Assert.IsTrue(refreshed.Success);
        }

        [TestMethod]
        public async Task StartSessionAsync_WithAllowMultiplePolicy_FiresNoAuditHook()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = CreateService(auditLogger: auditLogger);

            await service.StartSessionAsync(CreateJwtTokenService(), "user-1", "user", SignInClaims(), SignInRoles);
            await service.StartSessionAsync(CreateJwtTokenService(), "user-1", "user", SignInClaims(), SignInRoles);

            Assert.AreEqual(0, auditLogger.LoginCalls.Count);
            Assert.AreEqual(0, auditLogger.RefreshCalls.Count);
            Assert.AreEqual(0, auditLogger.ConcurrentSessionsRevokedCalls.Count);
            Assert.AreEqual(0, auditLogger.SessionsInvalidatedCalls.Count);
            Assert.AreEqual(0, auditLogger.SessionSupersededCalls.Count);
        }

        [TestMethod]
        public async Task StartSessionAsync_WithSingleSessionPolicy_FiresOnlyConcurrentSessionsRevokedWithTheCount()
        {
            RecordingAuditLogger auditLogger = new RecordingAuditLogger();
            RefreshTokenService service = CreateService(auditLogger: auditLogger, concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);
            JwtTokenService jwtTokenService = CreateJwtTokenService();
            await service.StartSessionAsync(jwtTokenService, "user-1", "user", SignInClaims(), SignInRoles);
            await service.StartSessionAsync(jwtTokenService, "user-1", "user", SignInClaims(), SignInRoles);

            await service.StartSessionAsync(jwtTokenService, "user-1", "user", SignInClaims(), SignInRoles);

            // The second start revoked the first, and the third the second: one family each.
            Assert.AreEqual(2, auditLogger.ConcurrentSessionsRevokedCalls.Count);
            Assert.IsTrue(auditLogger.ConcurrentSessionsRevokedCalls.All(call => call.Subject == "user-1" && call.InvalidatedFamilyCount == 1));
            Assert.AreEqual(0, auditLogger.LoginCalls.Count);
            Assert.AreEqual(0, auditLogger.RefreshCalls.Count);
            Assert.AreEqual(0, auditLogger.SessionsInvalidatedCalls.Count);
            Assert.AreEqual(0, auditLogger.SessionSupersededCalls.Count);
        }

        [TestMethod]
        public async Task StartSessionAsync_StoresSerializedClaimsAndRolesWithoutFamilyId()
        {
            RefreshTokenService service = CreateService();
            List<Claim> claims = SignInClaims();
            claims.Add(new Claim(EasyReasyClaimTypes.RefreshFamilyId, "caller-supplied"));

            SessionStartResult session = await service.StartSessionAsync(CreateJwtTokenService(), "user-1", "user", claims, SignInRoles);

            StoredRefreshToken stored = StoredRowOf(session.AuthResponse.RefreshToken!);
            Assert.AreEqual("user-1", stored.Subject);
            Assert.AreEqual("user", stored.AuthType);
            Assert.AreEqual(RefreshTokenClaims.SerializeClaims(SignInClaims()), stored.SerializedClaims);
            Assert.AreEqual(RefreshTokenClaims.SerializeRoles(SignInRoles), stored.SerializedRoles);
            Assert.IsFalse(
                RefreshTokenClaims.DeserializeClaims(stored.SerializedClaims).Any(claim => claim.Type == EasyReasyClaimTypes.RefreshFamilyId),
                "a stored row never holds a family id claim");
        }

        [TestMethod]
        public async Task StartSessionAsync_WithResolverRegistered_NeverCallsTheResolver()
        {
            FakeRefreshClaimsResolver resolver = new FakeRefreshClaimsResolver(_ => RefreshClaimsDecision.Deny());
            RefreshTokenService service = CreateService(resolver);

            SessionStartResult session = await service.StartSessionAsync(CreateJwtTokenService(), "user-1", "user", SignInClaims(), SignInRoles);

            Assert.AreEqual(0, resolver.Calls.Count);
            Assert.AreEqual("org-7", Decode(session.AuthResponse.Token).Claims.Single(claim => claim.Type == "active_org").Value);
        }

        [TestMethod]
        public async Task StartSessionAsync_WithSingleSessionPolicy_RetiresTheSubjectsEarlierFamilies()
        {
            RefreshTokenService service = CreateService(concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);
            JwtTokenService jwtTokenService = CreateJwtTokenService();

            SessionStartResult first = await service.StartSessionAsync(jwtTokenService, "user-1", "user", SignInClaims(), SignInRoles);
            SessionStartResult second = await service.StartSessionAsync(jwtTokenService, "user-1", "user", SignInClaims(), SignInRoles);

            Assert.IsTrue(StoredRowOf(first.AuthResponse.RefreshToken!).IsInvalidated);
            Assert.IsFalse(StoredRowOf(second.AuthResponse.RefreshToken!).IsInvalidated);
        }

        [TestMethod]
        public async Task StartSessionAsync_WithSingleSessionPolicyAndClaimsThatCannotBeStored_ThrowsAndKeepsTheEarlierSession()
        {
            RefreshTokenService service = CreateService(concurrentSessionPolicy: ConcurrentSessionPolicy.SingleSession);
            JwtTokenService jwtTokenService = CreateJwtTokenService();
            SessionStartResult earlier = await service.StartSessionAsync(jwtTokenService, "user-1", "user", SignInClaims(), SignInRoles);
            List<Claim> claimsWithANull = [new Claim("active_org", "org-7"), null!];

            await Assert.ThrowsExceptionAsync<NullReferenceException>(
                () => service.StartSessionAsync(jwtTokenService, "user-1", "user", claimsWithANull, SignInRoles));

            Assert.IsFalse(StoredRowOf(earlier.AuthResponse.RefreshToken!).IsInvalidated);
            Assert.AreEqual(1, _store.Tokens.Count);
        }

        [DataTestMethod]
        [DataRow("null token service")]
        [DataRow("null subject")]
        [DataRow("blank subject")]
        [DataRow("null auth type")]
        [DataRow("blank auth type")]
        [DataRow("null claims")]
        [DataRow("null roles")]
        public async Task StartSessionAsync_WithAMissingArgument_ThrowsAndStoresNothing(string missing)
        {
            RefreshTokenService service = CreateService();
            JwtTokenService jwtTokenService = CreateJwtTokenService();
            List<Claim> claims = SignInClaims();

            Func<Task<SessionStartResult>> start = missing switch
            {
                "null token service" => () => service.StartSessionAsync(null!, "user-1", "user", claims, SignInRoles),
                "null subject" => () => service.StartSessionAsync(jwtTokenService, null!, "user", claims, SignInRoles),
                "blank subject" => () => service.StartSessionAsync(jwtTokenService, " ", "user", claims, SignInRoles),
                "null auth type" => () => service.StartSessionAsync(jwtTokenService, "user-1", null!, claims, SignInRoles),
                "blank auth type" => () => service.StartSessionAsync(jwtTokenService, "user-1", " ", claims, SignInRoles),
                "null claims" => () => service.StartSessionAsync(jwtTokenService, "user-1", "user", null!, SignInRoles),
                "null roles" => () => service.StartSessionAsync(jwtTokenService, "user-1", "user", claims, null!),
                _ => throw new ArgumentOutOfRangeException(nameof(missing), missing, "no such case"),
            };

            Exception? thrown = null;
            try
            {
                await start();
            }
            catch (Exception exception)
            {
                thrown = exception;
            }

            Assert.IsInstanceOfType(thrown, typeof(ArgumentException), missing);
            Assert.AreEqual(0, _store.Tokens.Count, missing);
        }
    }
}
