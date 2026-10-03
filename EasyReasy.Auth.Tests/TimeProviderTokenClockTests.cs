using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Net;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// One <see cref="TimeProvider"/> registered in DI drives token issuing, refreshing and bearer validation.
    /// </summary>
    [TestClass]
    public class TimeProviderTokenClockTests
    {
        private const string Secret = "super_secret_key_12345_12345_12345";
        private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);
        private static readonly DateTimeOffset FakeStart = new DateTimeOffset(2031, 3, 1, 9, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void CreateToken_FakeClock_StampsNotBeforeAndIssuedAtFromIt()
        {
            JwtTokenService service = new JwtTokenService(Secret, null, null, new FakeTimeProvider(FakeStart));

            string token = service.CreateToken("user-1", "user", [], [], FakeStart.UtcDateTime.AddHours(1));

            JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.AreEqual(FakeStart.UtcDateTime, decoded.ValidFrom);
            Assert.AreEqual(FakeStart.ToUnixTimeSeconds().ToString(), decoded.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Iat).Value);
        }

        [TestMethod]
        public async Task Get_TokenIssuedAndValidatedOnOneFakeClock_AcceptedThenExpiresAsTheClockMoves()
        {
            FakeTimeProvider clock = new FakeTimeProvider(FakeStart);
            await using WebApplication app = await StartAppAsync(clock, registerClockBeforeAuth: false);
            string token = IssueToken(app, FakeStart.UtcDateTime.AddHours(1));

            Assert.AreEqual(HttpStatusCode.OK, await GetStatusAsync(app, token));

            clock.SetUtcNow(FakeStart.AddHours(1) + ClockSkew - TimeSpan.FromSeconds(1));
            Assert.AreEqual(HttpStatusCode.OK, await GetStatusAsync(app, token), "within the clock skew after expiry");

            clock.SetUtcNow(FakeStart.AddHours(1) + ClockSkew + TimeSpan.FromSeconds(1));
            Assert.AreEqual(HttpStatusCode.Unauthorized, await GetStatusAsync(app, token), "past the clock skew after expiry");
        }

        [TestMethod]
        public async Task Get_FakeClockBeforeTokensNotBefore_RejectedOutsideSkewAcceptedWithin()
        {
            FakeTimeProvider clock = new FakeTimeProvider(FakeStart);
            await using WebApplication app = await StartAppAsync(clock, registerClockBeforeAuth: true);
            string token = IssueToken(app, FakeStart.UtcDateTime.AddHours(1));

            clock.SetUtcNow(FakeStart - ClockSkew - TimeSpan.FromSeconds(1));
            Assert.AreEqual(HttpStatusCode.Unauthorized, await GetStatusAsync(app, token), "before nbf minus the skew");

            clock.SetUtcNow(FakeStart - ClockSkew + TimeSpan.FromSeconds(1));
            Assert.AreEqual(HttpStatusCode.OK, await GetStatusAsync(app, token), "within the skew before nbf");
        }

        [TestMethod]
        public async Task Get_TokenValidOnWallClockButExpiredOnFakeClock_Rejected()
        {
            // The case JwtBearer gets wrong on its own: it copies the DI clock onto its options but checks lifetime
            // against the wall clock.
            FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(2));
            await using WebApplication app = await StartAppAsync(clock, registerClockBeforeAuth: false);
            string token = new JwtTokenService(Secret).CreateToken("user-1", "user", [], [], DateTime.UtcNow.AddHours(1));

            Assert.AreEqual(HttpStatusCode.Unauthorized, await GetStatusAsync(app, token));
        }

        [TestMethod]
        public async Task Get_NoClockRegistered_FreshTokenAcceptedAndExpiredTokenRejected()
        {
            await using WebApplication app = await StartAppAsync(clock: null, registerClockBeforeAuth: false);
            DateTime utcNow = DateTime.UtcNow;
            string fresh = IssueToken(app, utcNow.AddHours(1));
            string expired = new JwtTokenService(Secret, null, null, new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-3)))
                .CreateToken("user-1", "user", [], [], utcNow.AddHours(-2));

            Assert.AreSame(TimeProvider.System, app.Services.GetRequiredService<TimeProvider>());
            Assert.AreEqual(HttpStatusCode.OK, await GetStatusAsync(app, fresh));
            Assert.AreEqual(HttpStatusCode.Unauthorized, await GetStatusAsync(app, expired));
        }

        [TestMethod]
        public async Task RefreshAsync_FakeClock_DecidesRefreshExpiryAndStampsTheNewAccessToken()
        {
            FakeTimeProvider clock = new FakeTimeProvider(FakeStart);
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyAuth(Secret);
            services.AddRefreshTokenService<FakeRefreshTokenStore>(refreshTokenLifetime: TimeSpan.FromDays(1), accessTokenLifetime: TimeSpan.FromMinutes(15));
            services.AddSingleton<TimeProvider>(clock);
            await using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();
            IRefreshTokenService refreshService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
            IJwtTokenService jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

            string first = await refreshService.CreateRefreshTokenAsync("user-1", "user", null, null);
            StoredRefreshToken stored = ((FakeRefreshTokenStore)scope.ServiceProvider.GetRequiredService<IRefreshTokenStore>()).Tokens.Values.Single();
            Assert.AreEqual(FakeStart.UtcDateTime, stored.CreatedAt);
            Assert.AreEqual(FakeStart.UtcDateTime.AddDays(1), stored.ExpiresAt);

            clock.Advance(TimeSpan.FromHours(23));
            RefreshResult refreshed = await refreshService.RefreshAsync(first, jwtTokenService);
            Assert.IsTrue(refreshed.Success);
            AuthResponse? authResponse = refreshed.AuthResponse;
            string? second = refreshed.NewRefreshToken;
            Assert.IsNotNull(authResponse);
            Assert.IsNotNull(second);
            JwtSecurityToken accessToken = new JwtSecurityTokenHandler().ReadJwtToken(authResponse.Token);
            Assert.AreEqual(FakeStart.UtcDateTime.AddHours(23), accessToken.ValidFrom);
            Assert.AreEqual(FakeStart.UtcDateTime.AddHours(23).AddMinutes(15), accessToken.ValidTo);

            clock.Advance(TimeSpan.FromDays(2));
            RefreshResult expired = await refreshService.RefreshAsync(second, jwtTokenService);
            Assert.AreEqual(RefreshFailureReason.TokenExpired, expired.FailureReason);
        }

        private static async Task<WebApplication> StartAppAsync(TimeProvider? clock, bool registerClockBeforeAuth)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            if (clock != null && registerClockBeforeAuth)
            {
                builder.Services.AddSingleton(clock);
            }

            builder.Services.AddEasyReasyAuth(Secret, options => options.ClockSkew = ClockSkew);

            if (clock != null && !registerClockBeforeAuth)
            {
                builder.Services.AddSingleton(clock);
            }

            WebApplication app = builder.Build();
            app.UseEasyReasyAuth(options => options.Enabled = false);
            app.MapGet("/whoami", (HttpContext context) => context.GetUserId()).RequireAuthorization();
            await app.StartAsync();
            return app;
        }

        private static string IssueToken(WebApplication app, DateTime expiresAt)
        {
            return app.Services.GetRequiredService<IJwtTokenService>().CreateToken("user-1", "user", [], [], expiresAt);
        }

        private static async Task<HttpStatusCode> GetStatusAsync(WebApplication app, string token)
        {
            using HttpClient client = ((TestServer)app.Services.GetRequiredService<IServer>()).CreateClient();
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
            using HttpResponseMessage response = await client.SendAsync(request);
            return response.StatusCode;
        }
    }
}
