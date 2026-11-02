using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Net;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class CookieTokenTests
    {
        private static readonly string[] FilePaths = ["/files"];

        private static string CookieHeader(string token) => $"{CookieTokenFallback.CookieName}={token}";

        [TestMethod]
        public async Task Get_ValidCookieOnListedPath_Authenticates()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("reader-1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Head_ValidCookieOnListedPath_Authenticates()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Head, "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        public async Task Head_NoCookie_Returns401()
        {
            // Without this, the HEAD test above would pass against an endpoint that never required a token.
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);

            using HttpResponseMessage response = await app.SendAsync(HttpMethod.Head, "/files/whoami");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        [DataRow("POST")]
        [DataRow("PUT")]
        [DataRow("DELETE")]
        public async Task NonReadMethod_ValidCookieOnListedPath_Returns401(string method)
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                new HttpMethod(method), "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Post_BearerHeaderOnListedPath_StillAuthenticates()
        {
            // The method rule bounds the cookie only, not the path: a header token works on every method.
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Post, "/files/whoami", authorizationHeader: $"Bearer {token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [TestMethod]
        [DataRow("/api/whoami")]
        [DataRow("/filesx/whoami")]
        public async Task Get_ValidCookieOnUnlistedPath_Returns401(string path)
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(HttpMethod.Get, path, cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_ExpiredCookieToken_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateExpiredToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertTokenWasReadAndRejected(response);
        }

        [TestMethod]
        public async Task Get_CookieTokenSignedWithOtherSecret_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateTokenSignedWithOtherSecret("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertTokenWasReadAndRejected(response);
        }

        [TestMethod]
        public async Task Get_BearerHeaderAndCookie_HeaderWins()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string headerToken = FallbackTokenTestApp.CreateToken("header-user");
            string cookieToken = FallbackTokenTestApp.CreateToken("cookie-user");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", authorizationHeader: $"Bearer {headerToken}", cookieHeader: CookieHeader(cookieToken));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("header-user", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_NonBearerHeaderAndValidCookie_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string cookieToken = FallbackTokenTestApp.CreateToken("cookie-user");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", authorizationHeader: "Basic dXNlcjpwYXNz", cookieHeader: CookieHeader(cookieToken));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_QueryTokenAndCookie_QueryTokenWins()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(FilePaths, cookieTokenPaths: FilePaths);
            string queryToken = FallbackTokenTestApp.CreateToken("query-user");
            string cookieToken = FallbackTokenTestApp.CreateToken("cookie-user");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, $"/files/whoami?access_token={queryToken}", cookieHeader: CookieHeader(cookieToken));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("query-user", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_InvalidQueryTokenAndValidCookie_Returns401()
        {
            // The query string supplied a token, so the cookie is not a second chance for it.
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(FilePaths, cookieTokenPaths: FilePaths);
            string queryToken = FallbackTokenTestApp.CreateExpiredToken("query-user");
            string cookieToken = FallbackTokenTestApp.CreateToken("cookie-user");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, $"/files/whoami?access_token={queryToken}", cookieHeader: CookieHeader(cookieToken));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertTokenWasReadAndRejected(response);
        }

        [TestMethod]
        public async Task Get_EmptyQueryTokenAndValidCookie_CookieAuthenticates()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(FilePaths, cookieTokenPaths: FilePaths);
            string cookieToken = FallbackTokenTestApp.CreateToken("cookie-user");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami?access_token=", cookieHeader: CookieHeader(cookieToken));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("cookie-user", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_QueryTokenOnPathListedOnlyForCookie_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.GetAsync($"/files/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_CookieAmongOtherCookies_Authenticates()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: $"theme=dark; {CookieHeader(token)}; _ga=\"GA1.2, odd value\"; flag");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("reader-1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_RepeatedCookie_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string userToken = FallbackTokenTestApp.CreateToken("reader-1");
            string tossedToken = FallbackTokenTestApp.CreateToken("attacker");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: $"{CookieHeader(tossedToken)}; {CookieHeader(userToken)}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_RepeatedIdenticalCookie_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: $"{CookieHeader(token)}; {CookieHeader(token)}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_CookieNameDifferingInCase_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: $"__secure-access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public void ReadToken_CookieSplitAcrossSeveralCookieHeaders_CountsEveryHeader()
        {
            // HTTP/2 may send each cookie in a header field of its own.
            CookieTokenFallback fallback = new CookieTokenFallback(FilePaths);

            Assert.AreEqual("token-a", fallback.ReadToken(CreateGetRequest("/files/a", "theme=dark", CookieHeader("token-a"))));
            Assert.IsNull(fallback.ReadToken(CreateGetRequest("/files/a", CookieHeader("token-a"), CookieHeader("token-b"))));
        }

        [TestMethod]
        public async Task Get_OptionUnset_CookieIgnored()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([]);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_OnlyQueryStringOptionSet_CookieIgnored()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_ConsumerAssignsOwnEvents_ConsumerHandlerRunsFirstAndCookieAuthenticates()
        {
            string? tokenSeenByConsumer = "not called";
            int consumerHandlerCalls = 0;
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                FilePaths,
                bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        Interlocked.Increment(ref consumerHandlerCalls);
                        tokenSeenByConsumer = context.Token;
                        return Task.CompletedTask;
                    },
                },
                cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("reader-1", await response.Content.ReadAsStringAsync());

            // Called once with both fallbacks on, so the events were wrapped once, and before the cookie was read.
            Assert.AreEqual(1, consumerHandlerCalls);
            Assert.IsNull(tokenSeenByConsumer);
        }

        [TestMethod]
        public async Task Get_ConsumerHandlerSetsToken_ConsumerTokenWinsOverCookie()
        {
            string consumerToken = FallbackTokenTestApp.CreateToken("consumer-user");
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                [],
                bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = consumerToken;
                        return Task.CompletedTask;
                    },
                },
                cookieTokenPaths: FilePaths);
            string cookieToken = FallbackTokenTestApp.CreateToken("cookie-user");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(cookieToken));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("consumer-user", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_ConsumerEventsSubclassOverridesMethods_CookieAndOverridesBothRun()
        {
            RecordingBearerEvents consumerEvents = new RecordingBearerEvents();
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                [],
                bearerOptions => bearerOptions.Events = consumerEvents,
                cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, consumerEvents.MessageReceivedCalls);
            Assert.AreEqual(1, consumerEvents.TokenValidatedCalls);
        }

        [TestMethod]
        public async Task Get_CookieWrittenUnderPathBase_AuthenticatesAtTheCookiesOwnPath()
        {
            // Where the writer and the reader have to agree: the writer puts PathBase into the cookie's Path,
            // which the browser matches against the full URL, while the reader compares Request.Path.
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths, pathBase: "/app");

            using HttpResponseMessage issued = await app.SendAsync(HttpMethod.Post, "/app/issue-cookie/reader-1");
            SetCookieHeaderValue cookie = SetCookieHeaderValue.Parse(issued.Headers.GetValues("Set-Cookie").Single());

            Assert.AreEqual("/app/files", cookie.Path.Value);

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, $"{cookie.Path.Value}/whoami", cookieHeader: $"{cookie.Name}={cookie.Value}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("reader-1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_ListedPathInDifferentCase_Authenticates()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);
            string token = FallbackTokenTestApp.CreateToken("reader-1");

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/FILES/whoami", cookieHeader: CookieHeader(token));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("reader-1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_EmptyCookieValue_TreatedAsNoToken()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([], cookieTokenPaths: FilePaths);

            using HttpResponseMessage response = await app.SendAsync(
                HttpMethod.Get, "/files/whoami", cookieHeader: CookieHeader(""));

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

            // A bare "Bearer" challenge: no token reached validation.
            string challenge = string.Join(", ", response.Headers.WwwAuthenticate.Select(header => header.ToString()));
            Assert.IsFalse(challenge.Contains("invalid_token"), challenge);
        }

        [TestMethod]
        public void ReadToken_RepeatedCookieInOneHeader_ReturnsNullAndSingleCookieReturnsToken()
        {
            CookieTokenFallback fallback = new CookieTokenFallback(FilePaths);

            Assert.AreEqual("token-a", fallback.ReadToken(CreateGetRequest("/files/a", $"theme=dark; {CookieHeader("token-a")}")));
            Assert.IsNull(fallback.ReadToken(CreateGetRequest("/files/a", $"{CookieHeader("token-a")}; {CookieHeader("token-b")}")));
            Assert.IsNull(fallback.ReadToken(CreateGetRequest("/files/a", $"{CookieHeader("token-a")}; {CookieHeader("token-a")}")));
        }

        private static HttpRequest CreateGetRequest(string path, params string[] cookieHeaders)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = path;
            context.Request.Headers.Cookie = new StringValues(cookieHeaders);
            return context.Request;
        }

        private static void AssertTokenWasReadAndRejected(HttpResponseMessage response)
        {
            // A token that was never read yields a bare "Bearer" challenge; one that was read and failed validation
            // names the error.
            string challenge = string.Join(", ", response.Headers.WwwAuthenticate.Select(header => header.ToString()));
            StringAssert.Contains(challenge, "invalid_token");
        }
    }
}
