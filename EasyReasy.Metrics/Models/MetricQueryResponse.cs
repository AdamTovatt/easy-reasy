namespace EasyReasy.Metrics.Models
{
    /// <summary>
    /// The response from a batch metric query, containing results and any errors that occurred.
    /// </summary>
    public class MetricQueryResponse
    {
        /// <summary>
        /// Gets or sets the snapshot results keyed by the metric key string.
        /// A value of <c>null</c> indicates no snapshot was found for that key.
        /// </summary>
        public Dictionary<string, MetricSnapshot?> Snapshots { get; set; } = new();

        /// <summary>
        /// Gets or sets the range results keyed by the metric key string.
        /// </summary>
        public Dictionary<string, IReadOnlyList<MetricDataPoint>> Ranges { get; set; } = new();

        /// <summary>
        /// Gets or sets the delta results keyed by the metric key string.
        /// </summary>
        public Dictionary<string, MetricDelta> Deltas { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of errors that occurred while processing individual requests.
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }
}
