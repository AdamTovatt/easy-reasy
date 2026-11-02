using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Tests for <see cref="HttpContextExtensions.SetAccessTokenCookie"/>, <see cref="HttpContextExtensions.ClearAccessTokenCookie"/>
    /// and <see cref="HttpContextExtensions.IsAccessTokenCookieEnabled"/>, asserting the exact <c>Set-Cookie</c> headers.
    /// </summary>
    [TestClass]
    public class AccessTokenCookieTests
    {
        private const string Secret = "super_secret_key_12345_12345_12345";
        private static readonly DateTimeOffset Now = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);

        [TestMethod]
        public void SetAccessTokenCookie_TwoPrefixesAndPathBase_WritesOneCookiePerPrefixWithFixedAttributes()
        {
            FakeTimeProvider clock = new FakeTimeProvider(Now);
            using ServiceProvider services = BuildServices(["/files", "/media"], clock);
            DefaultHttpContext context = CreateContext(services, pathBase: "/app");
            string token = CreateToken(clock, expiresAt: Now.AddMinutes(5));

            context.SetAccessTokenCookie(token);

            CollectionAssert.AreEqual(
                new string[]
                {
                    $"__Secure-access_token={token}; max-age=300; path=/app/files; secure; samesite=strict; httponly",
                    $"__Secure-access_token={token}; max-age=300; path=/app/media; secure; samesite=strict; httponly",
                },
                SetCookieHeaders(context));
        }

        [TestMethod]
        public void SetAccessTokenCookie_NoPathBase_PathIsThePrefix()
        {
            FakeTimeProvider clock = new FakeTimeProvider(Now);
            using ServiceProvider services = BuildServices(["/files"], clock);
            DefaultHttpContext context = CreateContext(services, pathBase: "");

            context.SetAccessTokenCookie(CreateToken(clock, expiresAt: Now.AddMinutes(5)));

            StringAssert.Contains(SetCookieHeaders(context).Single(), "; path=/files;");
        }

        [TestMethod]
        public void SetAccessTokenCookie_ClockMovedSinceIssue_MaxAgeIsSecondsLeftOnRegisteredClock()
        {
            FakeTimeProvider clock = new FakeTimeProvider(Now);
            using ServiceProvider services = BuildServices(["/files"], clock);
            DefaultHttpContext context = CreateContext(services, pathBase: "");
            string token = CreateToken(clock, expiresAt: Now.AddMinutes(5));

            // Half a second past a whole second: the cookie rounds down, so it never outlives the token.
            clock.Advance(TimeSpan.FromSeconds(100.5));
            context.SetAccessTokenCookie(token);

            StringAssert.Contains(SetCookieHeaders(context).Single(), "; max-age=199;");
        }

        [TestMethod]
        public void SetAccessTokenCookie_TokenAlreadyExpired_MaxAgeIsZero()
        {
            FakeTimeProvider clock = new FakeTimeProvider(Now);
            using ServiceProvider services = BuildServices(["/files"], clock);
            DefaultHttpContext context = CreateContext(services, pathBase: "");
            string token = CreateToken(clock, expiresAt: Now.AddMinutes(5));

            clock.Advance(TimeSpan.FromMinutes(10));
            context.SetAccessTokenCookie(token);

            StringAssert.Contains(SetCookieHeaders(context).Single(), "; max-age=0;");
        }

        [TestMethod]
        public void SetAccessTokenCookie_ExpiresAtIgnored_MaxAgeComesFromTheTokensExp()
        {
            // An unsigned token: the helper reads exp without validating, so the signature does not matter.
            FakeTimeProvider clock = new FakeTimeProvider(Now);
            using ServiceProvider services = BuildServices(["/files"], clock);
            DefaultHttpContext context = CreateContext(services, pathBase: "");
            string token = CreateUnsignedToken(exp: Now.AddSeconds(42).ToUnixTimeSeconds(), signatureLength: 4);

            context.SetAccessTokenCookie(token);

            StringAssert.Contains(SetCookieHeaders(context).Single(), "; max-age=42;");
        }

        [TestMethod]
        public void ClearAccessTokenCookie_TwoPrefixesAndPathBase_ExpiresOneCookiePerPrefix()
        {
            using ServiceProvider services = BuildServices(["/files", "/media"], new FakeTimeProvider(Now));
            DefaultHttpContext context = CreateContext(services, pathBase: "/app");

            context.ClearAccessTokenCookie();

            CollectionAssert.AreEqual(
                new string[]
                {
                    "__Secure-access_token=; max-age=0; path=/app/files; secure; samesite=strict; httponly",
                    "__Secure-access_token=; max-age=0; path=/app/media; secure; samesite=strict; httponly",
                },
                SetCookieHeaders(context));
        }

        [TestMethod]
        public void IsAccessTokenCookieEnabled_PathsListed_ReturnsTrue()
        {
            using ServiceProvider services = BuildServices(["/files"], new FakeTimeProvider(Now));

            Assert.IsTrue(CreateContext(services, pathBase: "").IsAccessTokenCookieEnabled());
        }

        [TestMethod]
        public void IsAccessTokenCookieEnabled_OptionUnset_ReturnsFalse()
        {
            using ServiceProvider services = BuildServices([], new FakeTimeProvider(Now));

            Assert.IsFalse(CreateContext(services, pathBase: "").IsAccessTokenCookieEnabled());
        }

        [TestMethod]
        public void IsAccessTokenCookieEnabled_AddEasyReasyAuthNotCalled_ReturnsFalse()
        {
            using ServiceProvider services = new ServiceCollection().BuildServiceProvider();

            Assert.IsFalse(CreateContext(services, pathBase: "").IsAccessTokenCookieEnabled());
            Assert.IsFalse(new DefaultHttpContext().IsAccessTokenCookieEnabled());
        }

        [TestMethod]
        [DataRow(nameof(HttpContextExtensions.SetAccessTokenCookie))]
        [DataRow(nameof(HttpContextExtensions.TrySetAccessTokenCookie))]
        [DataRow(nameof(HttpContextExtensions.ClearAccessTokenCookie))]
        public void CookieHelper_OptionUnset_ThrowsNamingTheOptionAndWritesNothing(string helperName)
        {
            FakeTimeProvider clock = new FakeTimeProvider(Now);
            using ServiceProvider services = BuildServices([], clock);
            DefaultHttpContext context = CreateContext(services, pathBase: "");
            string token = CreateToken(clock, expiresAt: Now.AddMinutes(5));

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => InvokeHelper(context, helperName, token));

            StringAssert.Contains(exception.Message, helperName);
            StringAssert.Contains(exception.Message, nameof(EasyReasyAuthOptions.CookieTokenPaths));
            Assert.AreEqual(0, SetCookieHeaders(context).Length);
        }

        [TestMethod]
        [DataRow(nameof(HttpContextExtensions.SetAccessTokenCookie))]
        [DataRow(nameof(HttpContextExtensions.TrySetAccessTokenCookie))]
        [DataRow(nameof(HttpContextExtensions.ClearAccessTokenCookie))]
        public void CookieHelper_AddEasyReasyAuthNotCalled_Throws(string helperName)
        {
            DefaultHttpContext context = new DefaultHttpContext();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => InvokeHelper(context, helperName, "a.b.c"));

            StringAssert.Contains(exception.Message, helperName);
        }

        [TestMethod]
        public void SetAccessTokenCookie_TokenWithoutExp_ThrowsArgumentExceptionAndWritesNothing()
        {
            using ServiceProvider services = BuildServices(["/files"], new FakeTimeProvider(Now));
            DefaultHttpContext context = CreateContext(services, pathBase: "");
            string token = CreateUnsignedToken(exp: null, signatureLength: 4);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => context.SetAccessTokenCookie(token));

            StringAssert.Contains(exception.Message, "exp");
            Assert.AreEqual(0, SetCookieHeaders(context).Length);
        }

        [TestMethod]
        public void SetAccessTokenCookie_NotAJwt_ThrowsArgumentException()
        {
            using ServiceProvider services = BuildServices(["/files"], new FakeTimeProvider(Now));
            DefaultHttpContext context = CreateContext(services, pathBase: "");

            Assert.ThrowsException<ArgumentException>(() => context.SetAccessTokenCookie("not-a-jwt"));
            Assert.AreEqual(0, SetCookieHeaders(context).Length);
        }

        [TestMethod]
        public void SetAccessTokenCookie_CookieOneByteOverLimit_ThrowsNamingSizeAndLimitAndWritesNothing()
        {
            using ServiceProvider services = BuildServices(["/files", "/media"], new FakeTimeProvider(Now));
            DefaultHttpContext context = CreateContext(services, pathBase: "");
            string token = CreateTokenWithCookieSize(4097);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => context.SetAccessTokenCookie(token));

            StringAssert.Contains(exception.Message, "4097");
            StringAssert.Contains(exception.Message, "4096");
            Assert.AreEqual(0, SetCookieHeaders(context).Length);
        }

        [TestMethod]
        public void SetAccessTokenCookie_CookieExactlyAtLimit_IsWritten()
        {
            using ServiceProvider services = BuildServices(["/files"], new FakeTimeProvider(Now));
            DefaultHttpContext context = CreateContext(services, pathBase: "");
            string token = CreateTokenWithCookieSize(4096);

            context.SetAccessTokenCookie(token);

            StringAssert.StartsWith(SetCookieHeaders(context).Single(), $"__Secure-access_token={token};");
        }

        [TestMethod]
        public void TrySetAccessTokenCookie_TokenFits_WritesCookieAndReturnsTrue()
        {
            FakeTimeProvider clock = new FakeTimeProvider(Now);
            RecordingLoggerProvider logs = new RecordingLoggerProvider();
            using ServiceProvider services = BuildServices(["/files"], clock, logs);
            DefaultHttpContext context = CreateContext(services, pathBase: "");
            string token = CreateTokenWithCookieSize(4096);

            Assert.IsTrue(context.TrySetAccessTokenCookie(token));

            StringAssert.StartsWith(SetCookieHeaders(context).Single(), $"__Secure-access_token={token}; max-age=300;");
            Assert.AreEqual(0, logs.Entries.Count);
        }

        [TestMethod]
        public void TrySetAccessTokenCookie_CookieOneByteOverLimit_ClearsEachPrefixLogsSizeAndLimitAndReturnsFalse()
        {
            RecordingLoggerProvider logs = new RecordingLoggerProvider();
            using ServiceProvider services = BuildServices(["/files", "/media"], new FakeTimeProvider(Now), logs);
            DefaultHttpContext context = CreateContext(services, pathBase: "");

            Assert.IsFalse(context.TrySetAccessTokenCookie(CreateTokenWithCookieSize(4097)));

            CollectionAssert.AreEqual(
                new string[]
                {
                    "__Secure-access_token=; max-age=0; path=/files; secure; samesite=strict; httponly",
                    "__Secure-access_token=; max-age=0; path=/media; secure; samesite=strict; httponly",
                },
                SetCookieHeaders(context));

            RecordingLoggerProvider.Entry entry = logs.Entries.Single();
            Assert.AreEqual(LogLevel.Error, entry.Level);
            Assert.AreEqual("EasyReasy.Auth.AccessTokenCookie", entry.Category);
            StringAssert.Contains(entry.Message, "4097");
            StringAssert.Contains(entry.Message, "4096");
        }

        [TestMethod]
        public void TrySetAccessTokenCookie_NoLoggerRegistered_StillClearsAndReturnsFalse()
        {
            using ServiceProvider services = BuildServices(["/files"], new FakeTimeProvider(Now));
            DefaultHttpContext context = CreateContext(services, pathBase: "");

            Assert.IsFalse(context.TrySetAccessTokenCookie(CreateTokenWithCookieSize(4097)));
            StringAssert.Contains(SetCookieHeaders(context).Single(), "max-age=0");
        }

        [TestMethod]
        public void TrySetAccessTokenCookie_TokenWithoutExp_StillThrows()
        {
            // Only the size is tolerated: a token without exp is a bug in the issuer, not something claims grow into.
            using ServiceProvider services = BuildServices(["/files"], new FakeTimeProvider(Now));
            DefaultHttpContext context = CreateContext(services, pathBase: "");

            Assert.ThrowsException<ArgumentException>(() => context.TrySetAccessTokenCookie(CreateUnsignedToken(exp: null, signatureLength: 4)));
            Assert.AreEqual(0, SetCookieHeaders(context).Length);
        }

        /// <summary>
        /// Calls the public cookie helper named <paramref name="helperName"/>, so one test can cover each in a row of its own.
        /// </summary>
        private static void InvokeHelper(HttpContext context, string helperName, string accessToken)
        {
            switch (helperName)
            {
                case nameof(HttpContextExtensions.SetAccessTokenCookie):
                    context.SetAccessTokenCookie(accessToken);
                    break;
                case nameof(HttpContextExtensions.TrySetAccessTokenCookie):
                    context.TrySetAccessTokenCookie(accessToken);
                    break;
                case nameof(HttpContextExtensions.ClearAccessTokenCookie):
                    context.ClearAccessTokenCookie();
                    break;
                default:
                    throw new ArgumentException($"No helper named {helperName}.", nameof(helperName));
            }
        }

        private static ServiceProvider BuildServices(IReadOnlyList<string> cookieTokenPaths, TimeProvider clock, ILoggerProvider? loggerProvider = null)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyAuth(Secret, options => options.CookieTokenPaths = cookieTokenPaths);
            services.AddSingleton(clock);

            if (loggerProvider != null)
            {
                services.AddLogging(logging => logging.AddProvider(loggerProvider));
            }

            return services.BuildServiceProvider();
        }

        private static DefaultHttpContext CreateContext(IServiceProvider services, string pathBase)
        {
            DefaultHttpContext context = new DefaultHttpContext { RequestServices = services };
            context.Request.PathBase = pathBase;
            return context;
        }

        private static string[] SetCookieHeaders(HttpContext context)
        {
            return context.Response.Headers[HeaderNames.SetCookie].Select(value => value ?? "").ToArray();
        }

        private static string CreateToken(TimeProvider clock, DateTimeOffset expiresAt)
        {
            return new JwtTokenService(Secret, null, null, clock).CreateToken("user-1", "user", [], [], expiresAt.UtcDateTime);
        }

        /// <summary>
        /// Builds a token whose cookie (name and value) is exactly <paramref name="cookieBytes"/> long, padding the
        /// signature segment, which the helper never checks.
        /// </summary>
        private static string CreateTokenWithCookieSize(int cookieBytes)
        {
            long exp = Now.AddMinutes(5).ToUnixTimeSeconds();
            int unpaddedLength = AccessTokenCookie.CookieName.Length + CreateUnsignedToken(exp, signatureLength: 0).Length;
            string token = CreateUnsignedToken(exp, signatureLength: cookieBytes - unpaddedLength);

            Assert.AreEqual(cookieBytes, AccessTokenCookie.CookieName.Length + token.Length);
            return token;
        }

        private static string CreateUnsignedToken(long? exp, int signatureLength)
        {
            string header = Base64UrlEncoding.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
            string payloadJson = exp == null
                ? $"{{\"{JwtRegisteredClaimNames.Sub}\":\"user-1\"}}"
                : $"{{\"{JwtRegisteredClaimNames.Sub}\":\"user-1\",\"{JwtRegisteredClaimNames.Exp}\":{exp}}}";
            string payload = Base64UrlEncoding.Encode(Encoding.UTF8.GetBytes(payloadJson));
            return $"{header}.{payload}.{new string('A', signatureLength)}";
        }
    }
}
