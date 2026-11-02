using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Tests for the one events wrapper the query-string and cookie fallbacks share.
    /// </summary>
    [TestClass]
    public class FallbackTokenBearerEventsTests
    {
        [TestMethod]
        public void FallbackTokenBearerEvents_EveryVirtualEvent_IsOverridden()
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
                MethodInfo? implementation = typeof(FallbackTokenBearerEvents).GetMethod(
                    virtualEvent.Name, virtualEvent.GetParameters().Select(parameter => parameter.ParameterType).ToArray());

                Assert.IsNotNull(implementation, virtualEvent.Name);
                Assert.AreEqual(typeof(FallbackTokenBearerEvents), implementation.DeclaringType, virtualEvent.Name);
            }
        }

        [TestMethod]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public void ResolveBearerOptions_EventsTypeSet_ThrowsNamingEachOptionSet(bool queryStringSet, bool cookieSet)
        {
            using ServiceProvider provider = BuildProviderWithEventsType(
                queryStringSet ? ["/ws"] : [], cookieSet ? ["/files"] : []);

            IOptionsMonitor<JwtBearerOptions> monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
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
    }
}
