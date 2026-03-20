using Microsoft.AspNetCore.Http;
using System.Net;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class ProgressiveDelayMiddlewareTests
    {
        [TestMethod]
        public void CalculateDelay_WithFailuresBelowThreshold_ShouldReturnZero()
        {
            // Act
            int delay = ProgressiveDelayMiddleware.CalculateDelay(5);

            // Assert
            Assert.AreEqual(0, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithFailuresAtThreshold_ShouldReturnZero()
        {
            // Act
            int delay = ProgressiveDelayMiddleware.CalculateDelay(ProgressiveDelayMiddleware.NoDelayThreshold);

            // Assert
            Assert.AreEqual(0, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithFailuresAboveThreshold_ShouldReturnIncrementalDelay()
        {
            // Arrange
            int failuresAboveThreshold = 3;
            int failureCount = ProgressiveDelayMiddleware.NoDelayThreshold + failuresAboveThreshold;

            // Act
            int delay = ProgressiveDelayMiddleware.CalculateDelay(failureCount);

            // Assert
            Assert.AreEqual(failuresAboveThreshold * ProgressiveDelayMiddleware.DelayIncrementMs, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithManyFailures_ShouldNotExceedMaxDelay()
        {
            // Arrange
            int failureCount = 100000;

            // Act
            int delay = ProgressiveDelayMiddleware.CalculateDelay(failureCount);

            // Assert
            Assert.AreEqual(ProgressiveDelayMiddleware.MaxDelayMs, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithExtremeFailureCount_ShouldNotOverflow()
        {
            // Arrange — would overflow int if not clamped
            int failureCount = int.MaxValue;

            // Act
            int delay = ProgressiveDelayMiddleware.CalculateDelay(failureCount);

            // Assert
            Assert.AreEqual(ProgressiveDelayMiddleware.MaxDelayMs, delay);
        }

        [TestMethod]
        public void CalculateDelay_WithZeroFailures_ShouldReturnZero()
        {
            // Act
            int delay = ProgressiveDelayMiddleware.CalculateDelay(0);

            // Assert
            Assert.AreEqual(0, delay);
        }

        [TestMethod]
        public void GetClientIp_WithZeroTrustedProxies_ShouldIgnoreForwardedHeader()
        {
            // Arrange
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 5.6.7.8";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            // Act
            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 0);

            // Assert — should use RemoteIpAddress, not the header
            Assert.AreEqual("192.168.1.1", ip);
        }

        [TestMethod]
        public void GetClientIp_WithOneTrustedProxy_ShouldReturnCorrectClientIp()
        {
            // Arrange — header: "fake-ip, real-ip" with 1 proxy → pick last entry
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "fake-ip, 10.0.0.1";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            // Act
            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 1);

            // Assert
            Assert.AreEqual("10.0.0.1", ip);
        }

        [TestMethod]
        public void GetClientIp_WithTwoTrustedProxies_ShouldReturnCorrectClientIp()
        {
            // Arrange — header: "real-client, proxy1" with 2 proxies → pick index 0
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.50, 192.168.68.103";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.68.107");

            // Act
            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 2);

            // Assert
            Assert.AreEqual("203.0.113.50", ip);
        }

        [TestMethod]
        public void GetClientIp_WithTwoTrustedProxies_ShouldIgnoreAttackerSpoofedEntries()
        {
            // Arrange — header: "fake1, fake2, real-client, proxy1" with 2 proxies → pick index 2
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "fake1, fake2, 203.0.113.50, 192.168.68.103";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.68.107");

            // Act
            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 2);

            // Assert
            Assert.AreEqual("203.0.113.50", ip);
        }

        [TestMethod]
        public void GetClientIp_WithTrustedProxiesButNoHeader_ShouldFallbackToRemoteIp()
        {
            // Arrange
            DefaultHttpContext context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            // Act
            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 2);

            // Assert
            Assert.AreEqual("192.168.1.1", ip);
        }

        [TestMethod]
        public void GetClientIp_WithFewerHeaderEntriesThanTrustedProxies_ShouldFallbackToRemoteIp()
        {
            // Arrange — only 1 entry but trustedProxyCount=2, index would be negative
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "10.0.0.1";
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            // Act
            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 2);

            // Assert
            Assert.AreEqual("192.168.1.1", ip);
        }

        [TestMethod]
        public void GetClientIp_WithNoRemoteIpAndNoHeader_ShouldReturnUnknown()
        {
            // Arrange
            DefaultHttpContext context = new DefaultHttpContext();

            // Act
            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 0);

            // Assert
            Assert.AreEqual("unknown", ip);
        }

        [TestMethod]
        public void GetClientIp_WithWhitespaceInHeader_ShouldTrimEntries()
        {
            // Arrange
            DefaultHttpContext context = new DefaultHttpContext();
            context.Request.Headers["X-Forwarded-For"] = "  10.0.0.1  ,  192.168.1.1  ";
            context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

            // Act
            string ip = ProgressiveDelayMiddleware.GetClientIp(context, trustedProxyCount: 1);

            // Assert
            Assert.AreEqual("192.168.1.1", ip);
        }

        [TestMethod]
        public void Constructor_WithNegativeTrustedProxyCount_ShouldThrowArgumentOutOfRangeException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new ProgressiveDelayMiddleware(next: _ => Task.CompletedTask, trustedProxyCount: -1));
        }

        [TestMethod]
        public async Task InvokeAsync_WithUnauthorizedResponse_ShouldTrackFailure()
        {
            // Arrange
            ProgressiveDelayMiddleware middleware = new ProgressiveDelayMiddleware(
                next: context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                },
                trustedProxyCount: 0);

            DefaultHttpContext context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

            // Act — first request should have no delay
            DateTime before = DateTime.UtcNow;
            await middleware.InvokeAsync(context);
            TimeSpan elapsed = DateTime.UtcNow - before;

            // Assert — should complete quickly (no delay for first failure)
            Assert.IsTrue(elapsed.TotalMilliseconds < 1000);
        }

        [TestMethod]
        public async Task InvokeAsync_WithSuccessResponse_ShouldResetFailures()
        {
            // Arrange
            int callCount = 0;
            ProgressiveDelayMiddleware middleware = new ProgressiveDelayMiddleware(
                next: context =>
                {
                    callCount++;
                    if (callCount <= 15)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }
                    else if (callCount == 16)
                    {
                        context.Response.StatusCode = StatusCodes.Status200OK;
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    return Task.CompletedTask;
                },
                trustedProxyCount: 0);

            IPAddress clientIp = IPAddress.Parse("10.0.0.1");

            // Act — accumulate 15 failures
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

            // Assert — should complete quickly (counter was reset, no delay)
            Assert.IsTrue(elapsed.TotalMilliseconds < 1000);
        }
    }
}
