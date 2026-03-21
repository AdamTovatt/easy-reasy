using Microsoft.AspNetCore.Http;
using System.Net;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class ProgressiveDelayMiddlewareTests
    {
        private static ProgressiveDelayOptions CreateDefaultOptions()
        {
            return new ProgressiveDelayOptions();
        }

        [TestMethod]
        public void CalculateDelay_WithFailuresBelowThreshold_ShouldReturnZero()
        {
            ProgressiveDelayOptions options = CreateDefaultOptions();

            int delay = ProgressiveDelayMiddleware.CalculateDelay(5, options);

            Assert.AreEqual(0, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithFailuresAtThreshold_ShouldReturnZero()
        {
            ProgressiveDelayOptions options = CreateDefaultOptions();

            int delay = ProgressiveDelayMiddleware.CalculateDelay(options.FreeFailures, options);

            Assert.AreEqual(0, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithFailuresAboveThreshold_ShouldReturnIncrementalDelay()
        {
            ProgressiveDelayOptions options = CreateDefaultOptions();
            int failuresAboveThreshold = 3;
            int failureCount = options.FreeFailures + failuresAboveThreshold;

            int delay = ProgressiveDelayMiddleware.CalculateDelay(failureCount, options);

            Assert.AreEqual(failuresAboveThreshold * (int)options.DelayIncrement.TotalMilliseconds, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithManyFailures_ShouldNotExceedMaxDelay()
        {
            ProgressiveDelayOptions options = CreateDefaultOptions();

            int delay = ProgressiveDelayMiddleware.CalculateDelay(100000, options);

            Assert.AreEqual((int)options.MaxDelay.TotalMilliseconds, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithExtremeFailureCount_ShouldNotOverflow()
        {
            ProgressiveDelayOptions options = CreateDefaultOptions();

            int delay = ProgressiveDelayMiddleware.CalculateDelay(int.MaxValue, options);

            Assert.AreEqual((int)options.MaxDelay.TotalMilliseconds, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithZeroFailures_ShouldReturnZero()
        {
            ProgressiveDelayOptions options = CreateDefaultOptions();

            int delay = ProgressiveDelayMiddleware.CalculateDelay(0, options);

            Assert.AreEqual(0, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithCustomValues_ShouldCalculateCorrectly()
        {
            int delay = ProgressiveDelayMiddleware.CalculateDelay(
                failureCount: 7, freeFailures: 5, delayIncrementMs: 1000, maxDelayMs: 10000);

            Assert.AreEqual(2000, delay);
        }

        [TestMethod]
        public void GetClientIp_WithZeroTrustedProxies_ShouldIgnoreForwardedHeader()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 5.6.7.8";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 0);

            Assert.AreEqual("192.168.1.1", ip);
        }

        [TestMethod]
        public void GetClientIp_WithOneTrustedProxy_ShouldReturnCorrectClientIp()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "fake-ip, 10.0.0.1";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 1);

            Assert.AreEqual("10.0.0.1", ip);
        }

        [TestMethod]
        public void GetClientIp_WithTwoTrustedProxies_ShouldReturnCorrectClientIp()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.50, 192.168.68.103";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.68.107");

            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 2);

            Assert.AreEqual("203.0.113.50", ip);
        }

        [TestMethod]
        public void GetClientIp_WithTwoTrustedProxies_ShouldIgnoreAttackerSpoofedEntries()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "fake1, fake2, 203.0.113.50, 192.168.68.103";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.68.107");

            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 2);

            Assert.AreEqual("203.0.113.50", ip);
        }

        [TestMethod]
        public void GetClientIp_WithTrustedProxiesButNoHeader_ShouldFallbackToRemoteIp()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 2);

            Assert.AreEqual("192.168.1.1", ip);
        }

        [TestMethod]
        public void GetClientIp_WithFewerHeaderEntriesThanTrustedProxies_ShouldFallbackToRemoteIp()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "10.0.0.1";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 2);

            Assert.AreEqual("192.168.1.1", ip);
        }

        [TestMethod]
        public void GetClientIp_WithNoRemoteIpAndNoHeader_ShouldReturnUnknown()
        {
            DefaultHttpContext context = new DefaultHttpContext();

            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 0);

            Assert.AreEqual("unknown", ip);
        }

        [TestMethod]
        public void GetClientIp_WithWhitespaceInHeader_ShouldTrimEntries()
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "  10.0.0.1  ,  192.168.1.1  ";
            context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 1);

            Assert.AreEqual("192.168.1.1", ip);
        }

        [TestMethod]
        public async Task InvokeAsync_WithUnauthorizedResponse_ShouldTrackFailure()
        {
            ProgressiveDelayMiddleware middleware = new ProgressiveDelayMiddleware(
                next: ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
                options: CreateDefaultOptions());

            DefaultHttpContext requestContext = new DefaultHttpContext();
            requestContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            DateTime before = DateTime.UtcNow;
            await middleware.InvokeAsync(requestContext);
            TimeSpan elapsed = DateTime.UtcNow - before;

            Assert.IsTrue(elapsed.TotalMilliseconds < 1000);
        }

        [TestMethod]
        public async Task InvokeAsync_WithSuccessResponse_ShouldResetFailures()
        {
            int callCount = 0;
            ProgressiveDelayMiddleware middleware = new ProgressiveDelayMiddleware(
                next: ctx =>
                {
                    callCount++;
                    if (callCount <= 15)
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }
                    else if (callCount == 16)
                    {
                        ctx.Response.StatusCode = StatusCodes.Status200OK;
                    }
                    else
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    return Task.CompletedTask;
                },
                options: CreateDefaultOptions());

            IPAddress clientIp = IPAddress.Parse("10.0.0.1");

            // Accumulate 15 failures
            for (int i = 0; i < 15; i++)
            {
                DefaultHttpContext failContext = new DefaultHttpContext();
                failContext.Connection.RemoteIpAddress = clientIp;
                await middleware.InvokeAsync(failContext);
            }

            // Success resets the counter
            DefaultHttpContext successContext = new DefaultHttpContext();
            successContext.Connection.RemoteIpAddress = clientIp;
            await middleware.InvokeAsync(successContext);

            // Next failure should have no delay (counter was reset)
            DefaultHttpContext nextFailContext = new DefaultHttpContext();
            nextFailContext.Connection.RemoteIpAddress = clientIp;

            DateTime before = DateTime.UtcNow;
            await middleware.InvokeAsync(nextFailContext);
            TimeSpan elapsed = DateTime.UtcNow - before;

            Assert.IsTrue(elapsed.TotalMilliseconds < 1000);
        }

        [TestMethod]
        public async Task InvokeAsync_WithStaleEntry_ShouldIgnoreOldFailures()
        {
            FakeTimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            ProgressiveDelayOptions options = new ProgressiveDelayOptions
            {
                FreeFailures = 2,
                FailureEntryLifetime = TimeSpan.FromMinutes(10),
            };
            ProgressiveDelayMiddleware middleware = new ProgressiveDelayMiddleware(
                next: ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
                options: options,
                timeProvider: timeProvider);

            IPAddress clientIp = IPAddress.Parse("10.0.0.1");

            // Accumulate failures past the threshold
            for (int i = 0; i < 5; i++)
            {
                DefaultHttpContext failContext = new DefaultHttpContext();
                failContext.Connection.RemoteIpAddress = clientIp;
                await middleware.InvokeAsync(failContext);
            }

            // Advance time past the entry lifetime
            timeProvider.Advance(TimeSpan.FromMinutes(15));

            // Next request should have no delay because the entry is stale
            DefaultHttpContext nextContext = new DefaultHttpContext();
            nextContext.Connection.RemoteIpAddress = clientIp;

            DateTime before = DateTime.UtcNow;
            await middleware.InvokeAsync(nextContext);
            TimeSpan elapsed = DateTime.UtcNow - before;

            Assert.IsTrue(elapsed.TotalMilliseconds < 1000);
        }

        /// <summary>
        /// A fake <see cref="TimeProvider"/> that allows advancing time manually for testing.
        /// </summary>
        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _utcNow;

            public FakeTimeProvider(DateTimeOffset startTime)
            {
                _utcNow = startTime;
            }

            public override DateTimeOffset GetUtcNow() => _utcNow;

            public void Advance(TimeSpan duration)
            {
                _utcNow += duration;
            }
        }
    }
}
