using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Middleware that applies a progressive delay to repeated unauthorized requests from the same IP address.
    /// The first <see cref="NoDelayThreshold"/> failed requests have no delay, then the delay increases
    /// by <see cref="DelayIncrementMs"/> per additional failure, up to <see cref="MaxDelayMs"/>.
    /// </summary>
    public class ProgressiveDelayMiddleware
    {
        // Some might point out that this dictionary is never cleared which means someone could make many many requests
        // and fill it leading to a large amount of memory being consumed. This is not really true since we only store a short
        // ip string for each ip that makes a request that fails, it doesn't really matter how many requests they make after that
        // we store an int for them too but that's like 32 bits. Even if they somehow had the worlds largest bot network of
        // 100 000 different servers to attack this little api for some absolutely absurd reason it wouldn't really make a dent
        // in the memory usage compared to the memory that is just consumed by the runtime anyway
        private readonly ConcurrentDictionary<string, int> _failures = new ConcurrentDictionary<string, int>();
        private readonly RequestDelegate _next;
        private readonly int _trustedProxyCount;

        /// <summary>
        /// The number of failed requests before progressive delays begin.
        /// </summary>
        internal const int NoDelayThreshold = 10;

        /// <summary>
        /// The delay increment in milliseconds per failure beyond <see cref="NoDelayThreshold"/>.
        /// </summary>
        internal const int DelayIncrementMs = 500;

        /// <summary>
        /// The maximum delay in milliseconds that can be applied.
        /// </summary>
        internal const int MaxDelayMs = 30000;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressiveDelayMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="trustedProxyCount">
        /// The number of trusted reverse proxies between the client and this application.
        /// When set to 0 (default), the <c>X-Forwarded-For</c> header is ignored and
        /// <see cref="HttpContext.Connection"/> <c>RemoteIpAddress</c> is used directly.
        /// When set to N, the Nth entry from the right of the <c>X-Forwarded-For</c> header is used.
        /// </param>
        public ProgressiveDelayMiddleware(RequestDelegate next, int trustedProxyCount = 0)
        {
            if (trustedProxyCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trustedProxyCount), "Must be non-negative.");
            }

            _next = next;
            _trustedProxyCount = trustedProxyCount;
        }

        /// <summary>
        /// Processes the HTTP request and applies a progressive delay for repeated unauthorized requests.
        /// The delay is applied before the response is sent to the client.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            string ip = GetClientIp(context, _trustedProxyCount);

            // Check if this IP has accumulated enough failures to warrant a delay before processing
            if (_failures.TryGetValue(ip, out int currentFailures) && currentFailures >= NoDelayThreshold)
            {
                await Task.Delay(CalculateDelay(currentFailures));
            }

            await _next(context);

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                _failures.AddOrUpdate(ip, 1, (_, count) => count + 1);
            }
            else
            {
                _failures.TryRemove(ip, out _);
            }
        }

        /// <summary>
        /// Calculates the delay in milliseconds for the given failure count.
        /// Returns 0 for failure counts at or below <see cref="NoDelayThreshold"/>,
        /// and caps at <see cref="MaxDelayMs"/>.
        /// </summary>
        /// <param name="failureCount">The number of accumulated failures.</param>
        /// <returns>The delay in milliseconds.</returns>
        internal static int CalculateDelay(int failureCount)
        {
            if (failureCount <= NoDelayThreshold)
            {
                return 0;
            }

            int excessFailures = Math.Min(failureCount - NoDelayThreshold, MaxDelayMs / DelayIncrementMs);
            return excessFailures * DelayIncrementMs;
        }

        /// <summary>
        /// Extracts the client IP address from the HTTP context.
        /// When <paramref name="trustedProxyCount"/> is 0, the <c>X-Forwarded-For</c> header is ignored
        /// and the connection's remote IP address is used directly.
        /// When greater than 0, the entry at position <c>parts.Length - trustedProxyCount</c> from the
        /// <c>X-Forwarded-For</c> header is used, skipping over the trusted proxy entries at the right.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="trustedProxyCount">The number of trusted reverse proxies.</param>
        /// <returns>The client IP address string.</returns>
        internal static string GetClientIp(HttpContext context, int trustedProxyCount)
        {
            if (trustedProxyCount > 0)
            {
                string? forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwarded))
                {
                    string[] parts = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    // Header: "client, proxy1, proxy2" — trusted proxies occupy the rightmost N entries.
                    // The real client IP (as seen by the outermost proxy) is at index parts.Length - N.
                    int clientIndex = parts.Length - trustedProxyCount;
                    if (clientIndex >= 0 && clientIndex < parts.Length)
                    {
                        return parts[clientIndex];
                    }
                }
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
