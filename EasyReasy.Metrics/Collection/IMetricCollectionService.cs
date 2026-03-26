namespace EasyReasy.Metrics.Collection
{
    /// <summary>
    /// Service that orchestrates the collection of all registered metric collectors,
    /// storing results via the repository with error isolation and deduplication.
    /// </summary>
    public interface IMetricCollectionService
    {
        /// <summary>
        /// Collects values from all registered metric collectors and stores them as snapshots.
        /// Each collector is invoked independently; a failure in one collector does not prevent
        /// the others from executing. Collection is skipped for metrics that were collected
        /// within the configured minimum time between collections.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        Task CollectAllMetricsAsync(CancellationToken cancellationToken = default);
    }
}
