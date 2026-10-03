using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
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
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

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
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);

            WebSocketClient client = app.Server.CreateWebSocketClient();

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                client.ConnectAsync(new Uri("ws://localhost/ws/city"), CancellationToken.None));
            StringAssert.Contains(exception.Message, "401");
        }

        [TestMethod]
        public async Task Get_ValidQueryTokenOnListedPath_Authenticates()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("player-1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_ValidQueryTokenOnUnlistedPath_Returns401()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/api/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_PathSharingOnlyAStringPrefixWithListedPath_Returns401()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/wsx/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_ExpiredQueryToken_Returns401()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string token = QueryStringTokenTestApp.CreateExpiredToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertTokenWasReadAndRejected(response);
        }

        [TestMethod]
        public async Task Get_QueryTokenSignedWithOtherSecret_Returns401()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string token = QueryStringTokenTestApp.CreateTokenSignedWithOtherSecret("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertTokenWasReadAndRejected(response);
        }

        [TestMethod]
        public async Task Get_ValidQueryTokenOnEachOfSeveralListedPaths_Authenticates()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(["/api", "/ws"]);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

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
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string headerToken = QueryStringTokenTestApp.CreateToken("header-user");
            string queryToken = QueryStringTokenTestApp.CreateToken("query-user");

            using HttpResponseMessage response = await app.GetAsync(
                $"/ws/whoami?access_token={queryToken}", authorizationHeader: $"Bearer {headerToken}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("header-user", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_NonBearerHeaderAndValidQueryToken_Returns401()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string queryToken = QueryStringTokenTestApp.CreateToken("query-user");

            using HttpResponseMessage response = await app.GetAsync(
                $"/ws/whoami?access_token={queryToken}", authorizationHeader: "Basic dXNlcjpwYXNz");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_RepeatedQueryParameter_Returns401()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(WebSocketPaths);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}&access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_OptionUnset_QueryTokenIgnored()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync([]);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_OptionUnset_HeaderTokenStillAuthenticates()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync([]);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync("/ws/whoami", authorizationHeader: $"Bearer {token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("player-1", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_ConsumerAssignsOwnEvents_FallbackAndConsumerHandlerBothRun()
        {
            int consumerHandlerCalls = 0;
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        Interlocked.Increment(ref consumerHandlerCalls);
                        return Task.CompletedTask;
                    },
                });
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("player-1", await response.Content.ReadAsStringAsync());
            Assert.AreEqual(1, consumerHandlerCalls);
        }

        [TestMethod]
        public async Task Get_ConsumerHandlerSetsToken_ConsumerTokenWins()
        {
            string consumerToken = QueryStringTokenTestApp.CreateToken("consumer-user");
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = consumerToken;
                        return Task.CompletedTask;
                    },
                });
            string queryToken = QueryStringTokenTestApp.CreateToken("query-user");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={queryToken}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("consumer-user", await response.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task Get_ConsumerHandlerFailsRequest_QueryTokenNotUsed()
        {
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Fail("rejected by the consumer");
                        return Task.CompletedTask;
                    },
                });
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [TestMethod]
        public async Task Get_ConsumerEventsSubclassOverridesMethods_FallbackAndOverridesBothRun()
        {
            RecordingBearerEvents consumerEvents = new RecordingBearerEvents();
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = consumerEvents);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

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
            await using QueryStringTokenTestApp app = await QueryStringTokenTestApp.StartAsync(
                WebSocketPaths,
                bearerOptions => bearerOptions.Events = sharedEvents);
            string token = QueryStringTokenTestApp.CreateToken("player-1");

            using HttpResponseMessage response = await app.GetAsync($"/ws/whoami?access_token={token}");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreSame(consumerHandler, sharedEvents.OnMessageReceived);
        }

        [TestMethod]
        public void QueryStringTokenBearerEvents_EveryVirtualEvent_IsOverridden()
        {
            // An event the wrapper does not override would run the wrapper's own no-op delegate instead of the
            // consumer's handler, silently. This fails the day JwtBearerEvents gains an event.
            MethodInfo[] virtualEvents = typeof(JwtBearerEvents)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.IsVirtual && !method.IsFinal && method.DeclaringType != typeof(object))
                .ToArray();

            Assert.IsTrue(virtualEvents.Length > 0);

            foreach (MethodInfo virtualEvent in virtualEvents)
            {
                MethodInfo? implementation = typeof(QueryStringTokenBearerEvents).GetMethod(
                    virtualEvent.Name, virtualEvent.GetParameters().Select(parameter => parameter.ParameterType).ToArray());

                Assert.IsNotNull(implementation, virtualEvent.Name);
                Assert.AreEqual(typeof(QueryStringTokenBearerEvents), implementation.DeclaringType, virtualEvent.Name);
            }
        }

        [TestMethod]
        public void ResolveBearerOptions_EventsTypeSet_Throws()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyAuth(QueryStringTokenTestApp.Secret, options => options.QueryStringTokenPaths = WebSocketPaths);
            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                bearerOptions => bearerOptions.EventsType = typeof(JwtBearerEvents));
            using ServiceProvider provider = services.BuildServiceProvider();

            IOptionsMonitor<JwtBearerOptions> monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
                monitor.Get(JwtBearerDefaults.AuthenticationScheme));
            StringAssert.Contains(exception.Message, nameof(EasyReasyAuthOptions.QueryStringTokenPaths));
        }

        [TestMethod]
        public void ResolveBearerOptions_EventsTypeSetAndOptionUnset_DoesNotThrow()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyAuth(QueryStringTokenTestApp.Secret);
            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                bearerOptions => bearerOptions.EventsType = typeof(JwtBearerEvents));
            using ServiceProvider provider = services.BuildServiceProvider();

            JwtBearerOptions bearerOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);

            Assert.AreEqual(typeof(JwtBearerEvents), bearerOptions.EventsType);
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
