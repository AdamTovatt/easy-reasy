using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Middleware that applies a progressive delay to repeated unauthorized requests from the same IP address.
    /// The first <see cref="ProgressiveDelayOptions.FreeFailures"/> failed requests have no delay,
    /// then the delay increases by <see cref="ProgressiveDelayOptions.DelayIncrement"/> per additional failure,
    /// up to <see cref="ProgressiveDelayOptions.MaxDelay"/>.
    /// Stale failure entries are evicted after <see cref="ProgressiveDelayOptions.FailureEntryLifetime"/>.
    /// </summary>
    public class ProgressiveDelayMiddleware
    {
        private readonly ConcurrentDictionary<string, FailureEntry> _failures = new ConcurrentDictionary<string, FailureEntry>();
        private readonly RequestDelegate _next;
        private readonly ProgressiveDelayOptions _options;
        private readonly TimeProvider _timeProvider;
        private long _lastSweepTicks;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressiveDelayMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="options">The progressive delay configuration options.</param>
        public ProgressiveDelayMiddleware(RequestDelegate next, ProgressiveDelayOptions options)
            : this(next, options, TimeProvider.System)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressiveDelayMiddleware"/> class
        /// with an explicit <see cref="TimeProvider"/> for testability.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="options">The progressive delay configuration options.</param>
        /// <param name="timeProvider">The time provider used for timestamping failure entries and eviction.</param>
        internal ProgressiveDelayMiddleware(RequestDelegate next, ProgressiveDelayOptions options, TimeProvider timeProvider)
        {
            options.Validate();
            _next = next;
            _options = options;
            _timeProvider = timeProvider;
            _lastSweepTicks = timeProvider.GetUtcNow().UtcTicks;
        }

        /// <summary>
        /// Processes the HTTP request and applies a progressive delay for repeated unauthorized requests.
        /// The delay is applied before the response is sent to the client.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            string ip = GetClientIp(context, _options.TrustedProxyCount);
            DateTimeOffset now = _timeProvider.GetUtcNow();

            // Check if this IP has accumulated enough failures to warrant a delay before processing
            if (_failures.TryGetValue(ip, out FailureEntry entry))
            {
                if (IsStale(entry, now))
                {
                    _failures.TryRemove(ip, out _);
                }
                else if (entry.Count >= _options.FreeFailures)
                {
                    int delayMs = CalculateDelay(entry.Count, _options);
                    await Task.Delay(delayMs);
                }
            }

            await _next(context);

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                _failures.AddOrUpdate(
                    ip,
                    _ => new FailureEntry(1, now),
                    (_, existing) => new FailureEntry(existing.Count + 1, now));
            }
            else
            {
                _failures.TryRemove(ip, out _);
            }

            SweepStaleEntriesIfNeeded(now);
        }

        /// <summary>
        /// Calculates the delay in milliseconds for the given failure count using the provided options.
        /// </summary>
        /// <param name="failureCount">The number of accumulated failures.</param>
        /// <param name="options">The progressive delay configuration options.</param>
        /// <returns>The delay in milliseconds.</returns>
        internal static int CalculateDelay(int failureCount, ProgressiveDelayOptions options)
        {
            return CalculateDelay(
                failureCount,
                options.FreeFailures,
                (int)options.DelayIncrement.TotalMilliseconds,
                (int)options.MaxDelay.TotalMilliseconds);
        }

        /// <summary>
        /// Calculates the delay in milliseconds for the given failure count and configuration.
        /// Returns 0 for failure counts at or below <paramref name="freeFailures"/>,
        /// and caps at <paramref name="maxDelayMs"/>.
        /// </summary>
        /// <param name="failureCount">The number of accumulated failures.</param>
        /// <param name="freeFailures">The number of failures before delays begin.</param>
        /// <param name="delayIncrementMs">The delay increment in milliseconds per failure beyond the threshold.</param>
        /// <param name="maxDelayMs">The maximum delay in milliseconds.</param>
        /// <returns>The delay in milliseconds.</returns>
        internal static int CalculateDelay(int failureCount, int freeFailures, int delayIncrementMs, int maxDelayMs)
        {
            if (failureCount <= freeFailures || delayIncrementMs == 0)
            {
                return 0;
            }

            int excessFailures = Math.Min(failureCount - freeFailures, maxDelayMs / Math.Max(delayIncrementMs, 1));
            return excessFailures * delayIncrementMs;
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

        private bool IsStale(FailureEntry entry, DateTimeOffset now)
        {
            return _options.FailureEntryLifetime > TimeSpan.Zero
                && (now - entry.LastUpdated) > _options.FailureEntryLifetime;
        }

        private void SweepStaleEntriesIfNeeded(DateTimeOffset now)
        {
            if (_options.FailureEntryLifetime <= TimeSpan.Zero)
            {
                return;
            }

            long nowTicks = now.UtcTicks;
            long lastSweepTicks = Interlocked.Read(ref _lastSweepTicks);

            // Only sweep if enough time has passed since the last sweep to avoid constant iteration
            if ((nowTicks - lastSweepTicks) < _options.FailureEntryLifetime.Ticks)
            {
                return;
            }

            // Attempt to claim the sweep — if another thread already updated, skip
            if (Interlocked.CompareExchange(ref _lastSweepTicks, nowTicks, lastSweepTicks) != lastSweepTicks)
            {
                return;
            }

            foreach (KeyValuePair<string, FailureEntry> kvp in _failures)
            {
                if (IsStale(kvp.Value, now))
                {
                    _failures.TryRemove(kvp.Key, out _);
                }
            }
        }

        /// <summary>
        /// Tracks the failure count and the time of the most recent failure for an IP address.
        /// </summary>
        /// <param name="Count">The number of accumulated failures.</param>
        /// <param name="LastUpdated">The time of the most recent failure.</param>
        internal readonly record struct FailureEntry(int Count, DateTimeOffset LastUpdated);
    }
}
