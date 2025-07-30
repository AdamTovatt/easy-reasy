using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Middleware that applies a progressive delay to repeated unauthorized requests from the same IP address.
    /// The first 10 failed requests have no delay, then the delay increases by 500ms per failure beyond 10.
    /// </summary>
    public class ProgressiveDelayMiddleware
    {
        // Some might point out that this dicitionary is never cleared which means someone could make many many requests
        // and fill it leading to a large amount of memory being consumed. This is not really true since we only store a short
        // ip string for each ip that makes a request that fails, it doesn't really matter how many requests they make after that
        // we store an int for them too but that's like 32 bits. Even if they somehow had the worlds largest bot network of
        // 100 000 different servers to attack this little api for some absolutely absurd reason it wouldn't really make a dent
        // in the memory usage compared to the memory that is just consumed by the runtime anyway
        private readonly ConcurrentDictionary<string, int> _failures = new ConcurrentDictionary<string, int>();
        private readonly RequestDelegate _next;
        private const int NoDelayThreshold = 10;
        private const int DelayIncrementMs = 500;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressiveDelayMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public ProgressiveDelayMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Processes the HTTP request and applies a progressive delay for repeated unauthorized requests.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                string ip = GetClientIp(context);
                int failures = _failures.AddOrUpdate(ip, 1, (_, count) => count + 1);

                int delayMs = 0;
                if (failures > NoDelayThreshold)
                    delayMs = (failures - NoDelayThreshold) * DelayIncrementMs;

                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }
            else
            {
                // Optionally reset on success
                string ip = GetClientIp(context);
                _failures.TryRemove(ip, out _);
            }
        }

        private static string GetClientIp(HttpContext context)
        {
            // Check for X-Forwarded-For header (reverse proxy)
            string? forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
                return forwarded.Split(',')[0].Trim();

            // Fallback to remote IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}