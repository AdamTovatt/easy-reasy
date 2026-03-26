namespace EasyReasy.Metrics.Models
{
    /// <summary>
    /// A request for the delta (change) between two points in time for a specific metric.
    /// </summary>
    public class DeltaRequest
    {
        /// <summary>
        /// Gets or sets the metric key to query.
        /// </summary>
        public required MetricKey MetricKey { get; init; }

        /// <summary>
        /// Gets or sets the target date for the current (more recent) snapshot.
        /// </summary>
        public required DateTime CurrentDate { get; init; }

        /// <summary>
        /// Gets or sets the target date for the previous (older) snapshot.
        /// </summary>
        public required DateTime PreviousDate { get; init; }
    }
}
