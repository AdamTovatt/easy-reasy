using Microsoft.AspNetCore.Http;
using System.Net;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class ProgressiveDelayMiddlewareTests
    {
        private static readonly IPAddress ClientIp = IPAddress.Parse("10.0.0.1");

        private static ProgressiveDelayOptions CreateDefaultOptions()
        {
            return new ProgressiveDelayOptions();
        }

        /// <summary>
        /// Creates a middleware whose delay grows by 500 ms per failure beyond <paramref name="freeFailures"/>.
        /// The step is wide so that one failure more or less is told apart well clear of timer overshoot.
        /// </summary>
        private static ProgressiveDelayMiddleware CreateMiddleware(int freeFailures, RequestDelegate next)
        {
            ProgressiveDelayOptions options = new ProgressiveDelayOptions
            {
                FreeFailures = freeFailures,
                DelayIncrement = TimeSpan.FromMilliseconds(500),
            };
            return new ProgressiveDelayMiddleware(next, options);
        }

        private static DefaultHttpContext CreateContext(bool authenticated = false)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = ClientIp;
            if (authenticated)
            {
                ClaimsIdentity identity = new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
                    authenticationType: "TestAuth");
                context.User = new ClaimsPrincipal(identity);
            }

            return context;
        }

        private static async Task<TimeSpan> TimeInvocationAsync(ProgressiveDelayMiddleware middleware, HttpContext context)
        {
            DateTime before = DateTime.UtcNow;
            await middleware.InvokeAsync(context);
            return DateTime.UtcNow - before;
        }

        /// <summary>
        /// Refuses an authenticated caller the way an app refuses a deactivated account, and answers any other
        /// request with a plain 401, as a failed credential.
        /// </summary>
        private static Task RefuseAuthenticatedOtherwise401(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                context.RefuseAuthenticatedCaller();
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }

            return Task.CompletedTask;
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

            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext());

            Assert.IsTrue(elapsed.TotalMilliseconds < 1000);
        }

        [TestMethod]
        public async Task InvokeAsync_WithSuccessfulAuthentication_ShouldResetFailures()
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
                        // What an auth endpoint does when it issues a token.
                        ctx.Response.StatusCode = StatusCodes.Status200OK;
                        ctx.MarkAuthenticationSucceeded();
                    }
                    else
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    return Task.CompletedTask;
                },
                options: CreateDefaultOptions());

            // Accumulate 15 failures
            for (int i = 0; i < 15; i++)
            {
                await middleware.InvokeAsync(CreateContext());
            }

            // Success resets the counter
            await middleware.InvokeAsync(CreateContext());

            // Next failure should have no delay (counter was reset)
            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext());

            Assert.IsTrue(elapsed.TotalMilliseconds < 1000);
        }

        [DataTestMethod]
        [DataRow(StatusCodes.Status200OK)]
        [DataRow(StatusCodes.Status201Created)]
        [DataRow(StatusCodes.Status204NoContent)]
        [DataRow(StatusCodes.Status304NotModified)]
        [DataRow(StatusCodes.Status400BadRequest)]
        [DataRow(StatusCodes.Status404NotFound)]
        public async Task InvokeAsync_WithoutASuccessfulAuthentication_ShouldNotResetFailures(int interleavedStatusCode)
        {
            // No status code on its own clears the count — only an endpoint reporting that it authenticated
            // the caller does. 204 matters most: this library maps an anonymous logout endpoint by default
            // that always answers 204, so a status-only rule would hand an attacker a free counter reset
            // between guesses. 200 and 201 are here because a success that authenticated nobody is still not
            // an authentication.
            ProgressiveDelayOptions options = new ProgressiveDelayOptions
            {
                FreeFailures = 1,
                DelayIncrement = TimeSpan.FromMilliseconds(300),
            };

            int[] statusCodes = new int[]
            {
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status401Unauthorized,
                interleavedStatusCode,
                StatusCodes.Status401Unauthorized,
            };

            int callCount = 0;
            ProgressiveDelayMiddleware middleware = new ProgressiveDelayMiddleware(
                next: ctx =>
                {
                    ctx.Response.StatusCode = statusCodes[callCount];
                    callCount++;
                    return Task.CompletedTask;
                },
                options: options);

            // Two failures, then the request that must not clear them.
            for (int i = 0; i < 3; i++)
            {
                await middleware.InvokeAsync(CreateContext());
            }

            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext());

            Assert.IsTrue(
                elapsed.TotalMilliseconds >= 250,
                $"The accumulated failures should still be delaying the next attempt, but it took {elapsed.TotalMilliseconds} ms.");
        }

        [TestMethod]
        public async Task InvokeAsync_WithPlain401ToAuthenticatedCaller_ShouldCountFailure()
        {
            // Only the refusal call exempts a 401; an authenticated principal alone does not.
            ProgressiveDelayMiddleware middleware = CreateMiddleware(freeFailures: 0, next: ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            });

            for (int i = 0; i < 2; i++)
            {
                await middleware.InvokeAsync(CreateContext(authenticated: true));
            }

            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext(authenticated: true));

            Assert.IsTrue(
                elapsed.TotalMilliseconds >= 900,
                $"Two plain 401s should delay the next request by 1000 ms, but it took {elapsed.TotalMilliseconds} ms.");
        }

        [TestMethod]
        public async Task InvokeAsync_WithRefusedAuthenticatedCaller_ShouldNotCountFailure()
        {
            ProgressiveDelayMiddleware middleware = CreateMiddleware(freeFailures: 0, next: ctx =>
            {
                ctx.RefuseAuthenticatedCaller();
                return Task.CompletedTask;
            });

            for (int i = 0; i < 3; i++)
            {
                await middleware.InvokeAsync(CreateContext(authenticated: true));
            }

            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext(authenticated: true));

            Assert.IsTrue(
                elapsed.TotalMilliseconds < 400,
                $"Refusals of an authenticated caller should not count, but the next request took {elapsed.TotalMilliseconds} ms.");
        }

        [TestMethod]
        public async Task InvokeAsync_WithRefusedAuthenticatedCallerAfterFailures_ShouldStillDelayTheRefusal()
        {
            // Three failures with one free failure delay the next request by (3 - 1) * 500 = 1000 ms, and the
            // middleware cannot know before the request runs that it will be a refusal.
            ProgressiveDelayMiddleware middleware = CreateMiddleware(freeFailures: 1, next: RefuseAuthenticatedOtherwise401);

            for (int i = 0; i < 3; i++)
            {
                await middleware.InvokeAsync(CreateContext());
            }

            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext(authenticated: true));

            Assert.IsTrue(
                elapsed.TotalMilliseconds >= 900,
                $"The refusal should still wait out the 1000 ms delay, but it took {elapsed.TotalMilliseconds} ms.");
        }

        [TestMethod]
        public async Task InvokeAsync_WithRefusedAuthenticatedCallerAfterFailures_ShouldLeaveCountUnchanged()
        {
            // Three failures with one free failure delay the next request by (3 - 1) * 500 = 1000 ms. A refusal
            // that cleared the count would leave no delay; one that counted would make it 1500 ms.
            ProgressiveDelayMiddleware middleware = CreateMiddleware(freeFailures: 1, next: RefuseAuthenticatedOtherwise401);

            for (int i = 0; i < 3; i++)
            {
                await middleware.InvokeAsync(CreateContext());
            }

            await middleware.InvokeAsync(CreateContext(authenticated: true));
            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext());

            Assert.IsTrue(
                elapsed.TotalMilliseconds >= 900 && elapsed.TotalMilliseconds < 1300,
                $"The count should still be three (1000 ms delay), but the next request took {elapsed.TotalMilliseconds} ms.");
        }

        [TestMethod]
        public async Task InvokeAsync_WithRefusalOfAnonymousCaller_ShouldCountFailure()
        {
            // A refusal marked by mistake on a request without an authenticated principal must not let a
            // password guess escape the delay.
            ProgressiveDelayMiddleware middleware = CreateMiddleware(freeFailures: 0, next: ctx =>
            {
                ctx.RefuseAuthenticatedCaller();
                return Task.CompletedTask;
            });

            for (int i = 0; i < 2; i++)
            {
                await middleware.InvokeAsync(CreateContext());
            }

            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext());

            Assert.IsTrue(
                elapsed.TotalMilliseconds >= 900,
                $"Two refusals of an anonymous caller should delay the next request by 1000 ms, but it took {elapsed.TotalMilliseconds} ms.");
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

            // Accumulate failures past the threshold
            for (int i = 0; i < 5; i++)
            {
                await middleware.InvokeAsync(CreateContext());
            }

            // Advance time past the entry lifetime
            timeProvider.Advance(TimeSpan.FromMinutes(15));

            // Next request should have no delay because the entry is stale
            TimeSpan elapsed = await TimeInvocationAsync(middleware, CreateContext());

            Assert.IsTrue(elapsed.TotalMilliseconds < 1000);
        }
    }
}
