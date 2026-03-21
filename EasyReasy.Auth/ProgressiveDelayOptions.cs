namespace EasyReasy.Auth
{
    /// <summary>
    /// Configuration options for the progressive delay middleware.
    /// The middleware applies increasing delays to repeated unauthorized requests from the same IP address,
    /// which helps mitigate brute-force attacks.
    /// </summary>
    public class ProgressiveDelayOptions
    {
        /// <summary>
        /// The maximum value allowed for <see cref="DelayIncrement"/> and <see cref="MaxDelay"/>
        /// to prevent overflow when converting to milliseconds as <see cref="int"/>.
        /// </summary>
        private static readonly TimeSpan MaxAllowedTimeSpan = TimeSpan.FromMilliseconds(int.MaxValue);

        /// <summary>
        /// Whether progressive delay is enabled. Default is <c>true</c>.
        /// When disabled, the middleware is not added to the pipeline.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// The number of trusted reverse proxies between the client and this application.
        /// When set to 0 (default), the <c>X-Forwarded-For</c> header is ignored and the connection's
        /// remote IP address is used directly — this is the safe default for apps not behind a reverse proxy.
        /// When set to N, the middleware reads the Nth entry from the right of the <c>X-Forwarded-For</c>
        /// header to determine the client IP. For example, if your app is behind two nginx proxies,
        /// set this to 2.
        /// </summary>
        public int TrustedProxyCount { get; set; }

        /// <summary>
        /// The delay increment per failure beyond <see cref="FreeFailures"/>.
        /// Default is 500 milliseconds.
        /// </summary>
        public TimeSpan DelayIncrement { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// The number of failed requests before progressive delays begin.
        /// Default is 10.
        /// </summary>
        public int FreeFailures { get; set; } = 10;

        /// <summary>
        /// The maximum delay that can be applied to a single request.
        /// Default is 30 seconds.
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The lifetime of a failure tracking entry. After this duration with no new failures,
        /// the entry is considered stale and will be evicted. Default is 1 hour.
        /// Set to <see cref="TimeSpan.Zero"/> to disable eviction (entries persist indefinitely).
        /// </summary>
        public TimeSpan FailureEntryLifetime { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Validates the options and throws if any values are invalid.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any option value is out of range.</exception>
        internal void Validate()
        {
            if (TrustedProxyCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TrustedProxyCount), "Must be non-negative.");
            }

            if (DelayIncrement < TimeSpan.Zero || DelayIncrement > MaxAllowedTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(DelayIncrement),
                    $"Must be between zero and {MaxAllowedTimeSpan.TotalMilliseconds:N0} milliseconds.");
            }

            if (FreeFailures < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(FreeFailures), "Must be non-negative.");
            }

            if (MaxDelay < TimeSpan.Zero || MaxDelay > MaxAllowedTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxDelay),
                    $"Must be between zero and {MaxAllowedTimeSpan.TotalMilliseconds:N0} milliseconds.");
            }

            if (FailureEntryLifetime < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(FailureEntryLifetime), "Must be non-negative.");
            }
        }
    }
}
