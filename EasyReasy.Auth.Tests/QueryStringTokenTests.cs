using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class QueryStringTokenTests
    {
        private static readonly string[] WebSocketPaths = ["/ws"];

        [TestMethod]
        public async Task WebSocketConnect_ValidQueryTokenOnListedPath_ClaimsReachEndpoint()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            WebSocketClient client = app.Server.CreateWebSocketClient();
            using WebSocket socket = await client.ConnectAsync(
                new Uri($"ws://localhost/ws/city?access_token={token}"), CancellationToken.None);

            byte[] buffer = new byte[256];
            WebSocketReceiveResult received = await socket.ReceiveAsync(buffer, CancellationToken.None);

            Assert.AreEqual("player-1", Encoding.UTF8.GetString(buffer, 0, received.Count));
        }

        [TestMethod]
        public async Task WebSocketConnect_NoToken_HandshakeRejected()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);

            WebSocketClient client = app.Server.CreateWebSocketClient();

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                client.ConnectAsync(new Uri("ws://localhost/ws/city"), CancellationToken.None));
            StringAssert.Contains(exception.Message, "401");
        }

        [TestMethod]
        public async Task Get_ValidQueryTokenOnListedPath_Authenticates()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("player-1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_ValidQueryTokenOnUnlistedPath_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/api/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_PathSharingOnlyAStringPrefixWithListedPath_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/wsx/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_ExpiredQueryToken_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string token = FallbackTokenTestApp.CreateExpiredToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertTokenWasReadAndRejected(response);
        }

        [TestMethod]
        public async Task Get_QueryTokenSignedWithOtherSecret_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string token = FallbackTokenTestApp.CreateTokenSignedWithOtherSecret("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertTokenWasReadAndRejected(response);
        }

        [TestMethod]
        public async Task Get_ValidQueryTokenOnEachOfSeveralListedPaths_Authenticates()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(["/api", "/ws"]);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage first = await app.GetAsync($"/api/whoami?access_token={token}");
            using HttpResponseMessage second = await app.GetAsync($"/ws/whoami?access_token={token}");
            using HttpResponseMessage unlisted = await app.GetAsync($"/wsx/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, unlisted.StatusCode);
        }

        [TestMethod]
        public async Task Get_BearerHeaderAndQueryToken_HeaderWins()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string headerToken = FallbackTokenTestApp.CreateToken("header-user");
            string queryToken = FallbackTokenTestApp.CreateToken("query-user");

            using HttpResponseMessage response = await app.GetAsync(
                $"/ws/whoami?access_token={queryToken}", authorizationHeader: $"Bearer {headerToken}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("header-user", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_NonBearerHeaderAndValidQueryToken_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string queryToken = FallbackTokenTestApp.CreateToken("query-user");

            using HttpResponseMessage response = await app.GetAsync(
                $"/ws/whoami?access_token={queryToken}", authorizationHeader: "Basic dXNlcjpwYXNz");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_RepeatedQueryParameter_Returns401()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(WebSocketPaths);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}&access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_OptionUnset_QueryTokenIgnored()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([]);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_OptionUnset_HeaderTokenStillAuthenticates()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync([]);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync("/ws/whoami", authorizationHeader: $"Bearer {token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("player-1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_ConsumerAssignsOwnEvents_FallbackAndConsumerHandlerBothRun()
        {
            int consumerHandlerCalls = 0;
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        Interlocked.Increment(ref consumerHandlerCalls);
                        return Task.CompletedTask;
                    },
                });
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("player-1", await response.Content.ReadAsStringAsync());
            Assert.AreEqual(1, consumerHandlerCalls);
        }

        [TestMethod]
        public async Task Get_ConsumerHandlerSetsToken_ConsumerTokenWins()
        {
            string consumerToken = FallbackTokenTestApp.CreateToken("consumer-user");
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = consumerToken;
                        return Task.CompletedTask;
                    },
                });
            string queryToken = FallbackTokenTestApp.CreateToken("query-user");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={queryToken}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("consumer-user", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_ConsumerHandlerFailsRequest_QueryTokenNotUsed()
        {
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Fail("rejected by the consumer");
                        return Task.CompletedTask;
                    },
                });
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_ConsumerEventsSubclassOverridesMethods_FallbackAndOverridesBothRun()
        {
            RecordingBearerEvents consumerEvents = new RecordingBearerEvents();
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = consumerEvents);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("player-1", await response.Content.ReadAsStringAsync());
            Assert.AreEqual(1, consumerEvents.MessageReceivedCalls);
            Assert.AreEqual(1, consumerEvents.TokenValidatedCalls);
        }

        [TestMethod]
        public async Task Get_ConsumerSharesOneEventsInstance_InstanceLeftUnmodified()
        {
            Func<MessageReceivedContext, Task> consumerHandler = context => Task.CompletedTask;
            JwtBearerEvents sharedEvents = new JwtBearerEvents { OnMessageReceived = consumerHandler };
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = sharedEvents);
            string token = FallbackTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreSame(consumerHandler, sharedEvents.OnMessageReceived);
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
