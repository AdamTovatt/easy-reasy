namespace EasyReasy.Metrics.Models
{
    /// <summary>
    /// Represents the calculated change between two metric snapshots.
    /// </summary>
    public class MetricDelta
    {
        /// <summary>
        /// Gets the metric key identifying which metric this delta is for.
        /// </summary>
        public required MetricKey MetricKey { get; init; }

        /// <summary>
        /// Gets the current (more recent) snapshot.
        /// </summary>
        public required MetricSnapshot Current { get; init; }

        /// <summary>
        /// Gets the previous (older) snapshot.
        /// </summary>
        public required MetricSnapshot Previous { get; init; }

        /// <summary>
        /// Gets the absolute change in value (current - previous).
        /// </summary>
        public required decimal Change { get; init; }

        /// <summary>
        /// Gets the percentage change from previous to current.
        /// Null when the previous value is zero, since the percentage change is undefined.
        /// </summary>
        public required decimal? PercentageChange { get; init; }
    }
}
