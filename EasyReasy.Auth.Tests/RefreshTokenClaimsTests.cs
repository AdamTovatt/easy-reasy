using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class RefreshTokenClaimsTests
    {
        private const string TestSecret = "super_secret_key_12345_12345_12345";
        private const string TestIssuer = "test-issuer";

        [TestMethod]
        public void Serialize_WithClaims_ProducesCamelCaseTypeValuePairs()
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim("active_org_id", "org-42"),
                new Claim("email", "test@example.com"),
            };

            string? serialized = RefreshTokenClaims.SerializeClaims(claims);

            Assert.IsNotNull(serialized);
            // The format the refresh store round-trips is a JSON array of { type, value } pairs
            // serialized with the camelCase policy — assert the property names explicitly so a
            // future change to the contract is caught here.
            Assert.IsTrue(serialized.Contains("\"type\":\"active_org_id\""));
            Assert.IsTrue(serialized.Contains("\"value\":\"org-42\""));
        }

        [TestMethod]
        public void Serialize_WithEmptyClaims_ReturnsNull()
        {
            string? serialized = RefreshTokenClaims.SerializeClaims(new List<Claim>());
            Assert.IsNull(serialized);
        }

        [TestMethod]
        public void SerializeThenDeserialize_PreservesClaimTypesAndValues()
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim("active_org_id", "org-42"),
                new Claim("tenant_id", "tenant-7"),
            };

            string? serialized = RefreshTokenClaims.SerializeClaims(claims);
            IReadOnlyList<Claim> roundTripped = RefreshTokenClaims.DeserializeClaims(serialized);

            Assert.AreEqual(2, roundTripped.Count);
            Assert.AreEqual(1, roundTripped.Count(claim => claim.Type == "active_org_id" && claim.Value == "org-42"));
            Assert.AreEqual(1, roundTripped.Count(claim => claim.Type == "tenant_id" && claim.Value == "tenant-7"));
        }

        [TestMethod]
        public void Deserialize_WithNullOrEmpty_ReturnsEmptyList()
        {
            Assert.AreEqual(0, RefreshTokenClaims.DeserializeClaims(null).Count);
            Assert.AreEqual(0, RefreshTokenClaims.DeserializeClaims(string.Empty).Count);
        }

        [TestMethod]
        public void SerializeRoles_WithRoles_RoundTripsThroughDeserializeRoles()
        {
            List<string> roles = new List<string> { "admin", "write" };

            string? serialized = RefreshTokenClaims.SerializeRoles(roles);
            IReadOnlyList<string> roundTripped = RefreshTokenClaims.DeserializeRoles(serialized);

            CollectionAssert.AreEqual(roles, roundTripped.ToList());
        }

        [TestMethod]
        public void SerializeRoles_WithEmptyRoles_ReturnsNull()
        {
            string? serialized = RefreshTokenClaims.SerializeRoles(new List<string>());
            Assert.IsNull(serialized);
        }

        [TestMethod]
        public async Task SerializedClaim_SurvivesCreateRefreshTokenThenRefresh()
        {
            // This is the contract consumers depend on: a claim serialized here, seeded into a
            // refresh token, and passed through a refresh by a pass-through resolver must land on
            // the re-minted access token. It is exactly the active-organization claim scenario.
            FakeRefreshTokenStore store = new FakeRefreshTokenStore();
            JwtTokenService jwtTokenService = new JwtTokenService(TestSecret, TestIssuer);
            FakeRefreshClaimsResolver passThrough = new FakeRefreshClaimsResolver(
                context => RefreshClaimsDecision.Allow(context.StoredClaims, context.StoredRoles));
            RefreshTokenService service = new RefreshTokenService(store, claimsResolver: passThrough);

            string? serializedClaims = RefreshTokenClaims.SerializeClaims(new List<Claim> { new Claim("active_org_id", "org-42") });
            string rawToken = await service.CreateRefreshTokenAsync("user-1", "user", serializedClaims, null);

            RefreshResult result = await service.RefreshAsync(rawToken, jwtTokenService);

            Assert.IsTrue(result.Success);
            IEnumerable<Claim> issued = new JwtSecurityTokenHandler().ReadJwtToken(result.AuthResponse!.Token).Claims;
            Assert.AreEqual(1, issued.Count(claim => claim.Type == "active_org_id" && claim.Value == "org-42"));
        }
    }
}
