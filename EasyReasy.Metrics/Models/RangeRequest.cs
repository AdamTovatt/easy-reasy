namespace EasyReasy.Metrics.Models
{
    /// <summary>
    /// A request for sampled data points within a time range for a specific metric.
    /// </summary>
    public class RangeRequest
    {
        /// <summary>
        /// Gets the metric key to query.
        /// </summary>
        public required MetricKey MetricKey { get; init; }

        /// <summary>
        /// Gets the start of the time range (inclusive).
        /// </summary>
        public required DateTime Start { get; init; }

        /// <summary>
        /// Gets the end of the time range (inclusive).
        /// </summary>
        public required DateTime End { get; init; }

        /// <summary>
        /// Gets the maximum number of sampled data points to return.
        /// </summary>
        public required int MaxSamples { get; init; }
    }
}
