namespace EasyReasy.Metrics.Models
{
    /// <summary>
    /// A batch query request that can include multiple snapshot, range, and delta requests
    /// to be processed over a shared database session.
    /// </summary>
    public class MetricQueryRequest
    {
        /// <summary>
        /// Gets or sets the list of snapshot requests (latest value for each key).
        /// </summary>
        public List<SnapshotRequest> SnapshotRequests { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of range requests (sampled data points within a time range).
        /// </summary>
        public List<RangeRequest> RangeRequests { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of delta requests (change between two points in time).
        /// </summary>
        public List<DeltaRequest> DeltaRequests { get; set; } = new();
    }
}
