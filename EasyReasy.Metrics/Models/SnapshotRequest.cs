namespace EasyReasy.Metrics.Models
{
    /// <summary>
    /// A request for the latest snapshot of a specific metric.
    /// </summary>
    public class SnapshotRequest
    {
        /// <summary>
        /// Gets or sets the metric key to query.
        /// </summary>
        public required MetricKey MetricKey { get; init; }
    }
}
