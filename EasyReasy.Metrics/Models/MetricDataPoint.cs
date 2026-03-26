namespace EasyReasy.Metrics.Models
{
    /// <summary>
    /// A lightweight data point for sampled range queries, typically used for graphing.
    /// </summary>
    public class MetricDataPoint
    {
        /// <summary>
        /// Gets the UTC timestamp when this data point was collected.
        /// </summary>
        public required DateTime CollectedAt { get; init; }

        /// <summary>
        /// Gets the numeric value at the time of collection.
        /// </summary>
        public required decimal Value { get; init; }
    }
}
