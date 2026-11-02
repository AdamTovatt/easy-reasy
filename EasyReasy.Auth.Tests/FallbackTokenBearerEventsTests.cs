using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Reflection;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Tests for the one events wrapper the query-string and cookie fallbacks share.
    /// </summary>
    [TestClass]
    public class FallbackTokenBearerEventsTests
    {
        private const string QueryString = "query string";
        private const string Cookie = "cookie";

        /// <summary>
        /// Every event the forwarding tests below drive through a real host. The enumeration test holds this list
        /// equal to the virtual methods on <see cref="JwtBearerEvents"/>, so a new event cannot go untested.
        /// </summary>
        private static readonly string[] ForwardedEventNames =
        [
            nameof(JwtBearerEvents.MessageReceived),
            nameof(JwtBearerEvents.TokenValidated),
            nameof(JwtBearerEvents.AuthenticationFailed),
            nameof(JwtBearerEvents.Challenge),
            nameof(JwtBearerEvents.Forbidden),
        ];

        public static IEnumerable<object[]> EveryEventThroughEachFallback =>
            ForwardedEventNames.SelectMany(eventName => new[] { new object[] { eventName, QueryString }, new object[] { eventName, Cookie } });

        public static IEnumerable<object[]> EveryEvent => ForwardedEventNames.Select(eventName => new object[] { eventName });

        [TestMethod]
        public void FallbackTokenBearerEvents_EveryVirtualEvent_IsOverriddenAndCoveredByTheForwardingTests()
        {
            // An event the wrapper does not override would run the wrapper's own no-op delegate instead of the
            // consumer's handler, silently. One the forwarding tests do not drive could drop either the consumer's
            // events or a handler set in a later PostConfigure unnoticed. This fails the day JwtBearerEvents gains an event.
            MethodInfo[] virtualEvents = typeof(JwtBearerEvents)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.IsVirtual && !method.IsFinal && method.DeclaringType != typeof(object))
                .ToArray();

            CollectionAssert.AreEquivalent(ForwardedEventNames, virtualEvents.Select(method => method.Name).ToArray());

            foreach (MethodInfo virtualEvent in virtualEvents)
            {
                MethodInfo? implementation = typeof(FallbackTokenBearerEvents).GetMethod(
                    virtualEvent.Name, virtualEvent.GetParameters().Select(parameter => parameter.ParameterType).ToArray());

                Assert.IsNotNull(implementation, virtualEvent.Name);
                Assert.AreEqual(typeof(FallbackTokenBearerEvents), implementation.DeclaringType, virtualEvent.Name);
            }
        }

        [TestMethod]
        [DynamicData(nameof(EveryEventThroughEachFallback))]
        public async Task Request_HandlerSetInLaterPostConfigure_RunsOnceAfterConfigureHandlerWithFallbackWorking(string eventName, string fallback)
        {
            List<string> calls = new List<string>();
            await using FallbackTokenTestApp app = await StartAppAsync(
                fallback,
                configureBearer: bearerOptions =>
                {
                    bearerOptions.Events = new JwtBearerEvents();
                    AddRecordingHandler(bearerOptions.Events, eventName, calls, "configure");
                },
                postConfigureBearer: bearerOptions => AddRecordingHandler(bearerOptions.Events, eventName, calls, "post-configure"));

            using HttpResponseMessage response = await SendScenarioRequestAsync(app, eventName, fallback);

            // Each row shows the token arrived through the fallback: a 403 or a subject needs it authenticated, and a
            // 401 names invalid_token only when the expired token was read and rejected, not for a request without one.
            Assert.AreEqual(ExpectedStatus(eventName), response.StatusCode);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Assert.AreEqual("reader-1", await response.Content.ReadAsStringAsync());
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                StringAssert.Contains(response.Headers.WwwAuthenticate.ToString(), "error=\"invalid_token\"");
            }

            CollectionAssert.AreEqual(new[] { "configure", "post-configure" }, calls);
        }

        [TestMethod]
        [DynamicData(nameof(EveryEvent))]
        public async Task Request_ConfigureHandlerSettlesOutcome_LaterPostConfigureHandlerDoesNotRun(string eventName)
        {
            List<string> calls = new List<string>();
            await using FallbackTokenTestApp app = await StartAppAsync(
                QueryString,
                configureBearer: bearerOptions => bearerOptions.Events = CreateSettlingEvents(eventName, calls),
                postConfigureBearer: bearerOptions => AddRecordingHandler(bearerOptions.Events, eventName, calls, "post-configure"));

            using HttpResponseMessage _ = await SendScenarioRequestAsync(app, eventName, QueryString);

            CollectionAssert.AreEqual(new[] { "configure" }, calls);
        }

        [TestMethod]
        [DataRow(QueryString)]
        [DataRow(Cookie)]
        public async Task Get_LaterPostConfigureWrapsMessageReceivedAndCallsNoResult_HandlerRunsBeforeFallbackAndRequestIsUnauthorized(string fallback)
        {
            // A chained handler that declines the request before the fallback would fill a token.
            int wrappedHandlerCalls = 0;
            string? tokenSeenByWrappedHandler = "not run";
            await using FallbackTokenTestApp app = await StartAppAsync(
                fallback,
                postConfigureBearer: bearerOptions =>
                {
                    Func<MessageReceivedContext, Task> previousHandler = bearerOptions.Events.OnMessageReceived;
                    bearerOptions.Events.OnMessageReceived = async context =>
                    {
                        await previousHandler(context);
                        wrappedHandlerCalls++;
                        tokenSeenByWrappedHandler = context.Token;
                        context.NoResult();
                    };
                });

            using HttpResponseMessage response = await SendScenarioRequestAsync(app, nameof(JwtBearerEvents.MessageReceived), fallback);

            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.AreEqual(1, wrappedHandlerCalls);
            Assert.IsNull(tokenSeenByWrappedHandler);
        }

        [TestMethod]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task StartAsync_LaterPostConfigureAssignsNewEvents_FailsNamingEachOptionSet(bool queryStringSet, bool cookieSet)
        {
            OptionsValidationException exception = await Assert.ThrowsExceptionAsync<OptionsValidationException>(() =>
                FallbackTokenTestApp.StartAsync(
                    queryStringTokenPaths: queryStringSet ? ["/ws"] : [],
                    cookieTokenPaths: cookieSet ? ["/files"] : [],
                    postConfigureBearer: bearerOptions => bearerOptions.Events = new JwtBearerEvents()));

            Assert.AreEqual(queryStringSet, exception.Message.Contains(nameof(EasyReasyAuthOptions.QueryStringTokenPaths)));
            Assert.AreEqual(cookieSet, exception.Message.Contains(nameof(EasyReasyAuthOptions.CookieTokenPaths)));
            StringAssert.Contains(exception.Message, typeof(JwtBearerEvents).FullName);
            StringAssert.Contains(exception.Message, "Add handlers to the existing JwtBearerOptions.Events");
            StringAssert.Contains(exception.Message, "in Configure");
        }

        [TestMethod]
        public async Task StartAsync_LaterPostConfigureWrapsTheWrapperInForwardingSubclass_Fails()
        {
            OptionsValidationException exception = await Assert.ThrowsExceptionAsync<OptionsValidationException>(() =>
                FallbackTokenTestApp.StartAsync(
                    queryStringTokenPaths: ["/ws"],
                    postConfigureBearer: bearerOptions => bearerOptions.Events = new ForwardingBearerEvents(bearerOptions.Events)));

            StringAssert.Contains(exception.Message, typeof(ForwardingBearerEvents).FullName);
        }

        [TestMethod]
        public async Task StartAsync_LaterPostConfigureSetsEventsType_Fails()
        {
            OptionsValidationException exception = await Assert.ThrowsExceptionAsync<OptionsValidationException>(() =>
                FallbackTokenTestApp.StartAsync(
                    cookieTokenPaths: ["/files"],
                    postConfigureBearer: bearerOptions => bearerOptions.EventsType = typeof(JwtBearerEvents)));

            StringAssert.Contains(exception.Message, nameof(EasyReasyAuthOptions.CookieTokenPaths));
            StringAssert.Contains(exception.Message, nameof(JwtBearerOptions.EventsType));
        }

        [TestMethod]
        public async Task StartAsync_BothOptionsUnsetAndLaterPostConfigureAssignsNewEvents_StartsWithThoseEvents()
        {
            JwtBearerEvents assignedEvents = new JwtBearerEvents();
            await using FallbackTokenTestApp app = await FallbackTokenTestApp.StartAsync(
                postConfigureBearer: bearerOptions => bearerOptions.Events = assignedEvents);

            JwtBearerOptions bearerOptions = app.App.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);

            Assert.AreSame(assignedEvents, bearerOptions.Events);
        }

        [TestMethod]
        public void ResolveBearerOptions_OtherBearerSchemeAssignsOwnEvents_DoesNotFail()
        {
            // The fallback belongs to the default bearer scheme; a second scheme's own events must not trip its check.
            JwtBearerEvents otherEvents = new JwtBearerEvents();
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyAuth(FallbackTokenTestApp.Secret, options => options.QueryStringTokenPaths = ["/ws"]);
            services.AddAuthentication().AddJwtBearer("Other", bearerOptions => bearerOptions.Events = otherEvents);
            using ServiceProvider provider = services.BuildServiceProvider();

            JwtBearerOptions otherOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Other");

            Assert.AreSame(otherEvents, otherOptions.Events);
        }

        [TestMethod]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public void ResolveBearerOptions_EventsTypeSetInConfigure_FailsValidationNamingEachOptionSet(bool queryStringSet, bool cookieSet)
        {
            using ServiceProvider provider = BuildProviderWithEventsType(
                queryStringSet ? ["/ws"] : [], cookieSet ? ["/files"] : []);

            IOptionsMonitor<JwtBearerOptions> monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

            OptionsValidationException exception = Assert.ThrowsException<OptionsValidationException>(() =>
                monitor.Get(JwtBearerDefaults.AuthenticationScheme));
            Assert.AreEqual(queryStringSet, exception.Message.Contains(nameof(EasyReasyAuthOptions.QueryStringTokenPaths)));
            Assert.AreEqual(cookieSet, exception.Message.Contains(nameof(EasyReasyAuthOptions.CookieTokenPaths)));
        }

        [TestMethod]
        public void ResolveBearerOptions_EventsTypeSetAndBothOptionsUnset_DoesNotThrow()
        {
            using ServiceProvider provider = BuildProviderWithEventsType([], []);

            JwtBearerOptions bearerOptions = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);

            Assert.AreEqual(typeof(JwtBearerEvents), bearerOptions.EventsType);
        }

        private static ServiceProvider BuildProviderWithEventsType(IReadOnlyList<string> queryStringTokenPaths, IReadOnlyList<string> cookieTokenPaths)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddEasyReasyAuth(FallbackTokenTestApp.Secret, options =>
            {
                options.QueryStringTokenPaths = queryStringTokenPaths;
                options.CookieTokenPaths = cookieTokenPaths;
            });
            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                bearerOptions => bearerOptions.EventsType = typeof(JwtBearerEvents));
            return services.BuildServiceProvider();
        }

        private static Task<FallbackTokenTestApp> StartAppAsync(
            string fallback,
            Action<JwtBearerOptions>? configureBearer = null,
            Action<JwtBearerOptions>? postConfigureBearer = null)
        {
            return FallbackTokenTestApp.StartAsync(
                queryStringTokenPaths: fallback == QueryString ? ["/ws"] : [],
                cookieTokenPaths: fallback == Cookie ? ["/files"] : [],
                configureBearer: configureBearer,
                postConfigureBearer: postConfigureBearer);
        }

        /// <summary>
        /// Sends the request that raises <paramref name="eventName"/>, carrying its token only through
        /// <paramref name="fallback"/>: an expired token for the failure events, a user token on an
        /// API-key-only endpoint for <c>Forbidden</c>, and a valid one otherwise.
        /// </summary>
        private static Task<HttpResponseMessage> SendScenarioRequestAsync(FallbackTokenTestApp app, string eventName, string fallback)
        {
            bool failsValidation = eventName == nameof(JwtBearerEvents.AuthenticationFailed) || eventName == nameof(JwtBearerEvents.Challenge);
            string token = failsValidation ? FallbackTokenTestApp.CreateExpiredToken("reader-1") : FallbackTokenTestApp.CreateToken("reader-1");
            string endpoint = eventName == nameof(JwtBearerEvents.Forbidden) ? "apikey-only" : "whoami";

            return fallback == QueryString
                ? app.GetAsync($"/ws/{endpoint}?access_token={token}")
                : app.SendAsync(HttpMethod.Get, $"/files/{endpoint}", cookieHeader: $"{AccessTokenCookie.CookieName}={token}");
        }

        private static HttpStatusCode ExpectedStatus(string eventName)
        {
            return eventName switch
            {
                nameof(JwtBearerEvents.AuthenticationFailed) or nameof(JwtBearerEvents.Challenge) => HttpStatusCode.Unauthorized,
                nameof(JwtBearerEvents.Forbidden) => HttpStatusCode.Forbidden,
                _ => HttpStatusCode.OK,
            };
        }

        /// <summary>
        /// Chains a handler onto the existing <c>On...</c> delegate for <paramref name="eventName"/>, the pattern an
        /// app uses in a <c>PostConfigure</c>. In <c>MessageReceived</c> it records whether a token was already set,
        /// so a fallback that filled before the later handler ran shows up in <paramref name="calls"/>.
        /// </summary>
        private static void AddRecordingHandler(JwtBearerEvents events, string eventName, List<string> calls, string label)
        {
            switch (eventName)
            {
                case nameof(JwtBearerEvents.MessageReceived):
                    events.OnMessageReceived = Chain(events.OnMessageReceived, context => calls.Add(context.Token == null ? label : $"{label} saw a token"));
                    break;
                case nameof(JwtBearerEvents.TokenValidated):
                    events.OnTokenValidated = Chain(events.OnTokenValidated, context => calls.Add(label));
                    break;
                case nameof(JwtBearerEvents.AuthenticationFailed):
                    events.OnAuthenticationFailed = Chain(events.OnAuthenticationFailed, context => calls.Add(label));
                    break;
                case nameof(JwtBearerEvents.Challenge):
                    events.OnChallenge = Chain(events.OnChallenge, context => calls.Add(label));
                    break;
                case nameof(JwtBearerEvents.Forbidden):
                    events.OnForbidden = Chain(events.OnForbidden, context => calls.Add(label));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventName), eventName, null);
            }
        }

        private static Func<TContext, Task> Chain<TContext>(Func<TContext, Task> previous, Action<TContext> record)
        {
            return async context =>
            {
                await previous(context);
                record(context);
            };
        }

        /// <summary>
        /// Events whose <paramref name="eventName"/> handler records the call and decides the outcome, as the bearer
        /// handler understands it: a result, or a handled response for <c>Challenge</c>.
        /// </summary>
        private static JwtBearerEvents CreateSettlingEvents(string eventName, List<string> calls)
        {
            JwtBearerEvents events = new JwtBearerEvents();

            switch (eventName)
            {
                case nameof(JwtBearerEvents.MessageReceived):
                    events.OnMessageReceived = context => Settle(calls, () => context.Fail("settled"));
                    break;
                case nameof(JwtBearerEvents.TokenValidated):
                    events.OnTokenValidated = context => Settle(calls, () => context.Fail("settled"));
                    break;
                case nameof(JwtBearerEvents.AuthenticationFailed):
                    events.OnAuthenticationFailed = context => Settle(calls, () => context.Fail("settled"));
                    break;
                case nameof(JwtBearerEvents.Challenge):
                    events.OnChallenge = context => Settle(calls, context.HandleResponse);
                    break;
                case nameof(JwtBearerEvents.Forbidden):
                    events.OnForbidden = context => Settle(calls, () => context.Fail("settled"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventName), eventName, null);
            }

            return events;
        }

        private static Task Settle(List<string> calls, Action settle)
        {
            calls.Add("configure");
            settle();
            return Task.CompletedTask;
        }
    }
}
