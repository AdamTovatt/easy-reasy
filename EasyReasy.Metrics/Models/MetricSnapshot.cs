namespace EasyReasy.Metrics.Models
{
    /// <summary>
    /// Represents a single metric value recorded at a specific point in time.
    /// </summary>
    public class MetricSnapshot
    {
        /// <summary>
        /// Gets the unique database identifier for this snapshot.
        /// </summary>
        public required long Id { get; init; }

        /// <summary>
        /// Gets the metric key identifying which metric this snapshot belongs to.
        /// </summary>
        public required MetricKey MetricKey { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when this metric value was collected.
        /// </summary>
        public required DateTime CollectedAt { get; init; }

        /// <summary>
        /// Gets the numeric value of the metric at the time of collection.
        /// </summary>
        public required decimal Value { get; init; }
    }
}
