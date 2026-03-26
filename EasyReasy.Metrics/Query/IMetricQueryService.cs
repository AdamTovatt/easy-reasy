using EasyReasy.Metrics.Models;

namespace EasyReasy.Metrics.Query
{
    /// <summary>
    /// Service for querying metric snapshots, providing both individual and batch query methods.
    /// Individual methods delegate to the repository; the batch method processes multiple requests
    /// over a shared database session.
    /// </summary>
    public interface IMetricQueryService
    {
        /// <summary>
        /// Gets the most recent snapshot for the specified metric.
        /// </summary>
        /// <param name="key">The metric key to query.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>The latest snapshot, or <c>null</c> if no snapshots exist for this key.</returns>
        Task<MetricSnapshot?> GetLatestAsync(MetricKey key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the snapshot closest to the specified target date for the given metric.
        /// </summary>
        /// <param name="key">The metric key to query.</param>
        /// <param name="targetDate">The target date to find the closest snapshot to.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>The closest snapshot, or <c>null</c> if no snapshots exist for this key.</returns>
        Task<MetricSnapshot?> GetClosestAsync(MetricKey key, DateTime targetDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets sampled data points within a time range for the specified metric, suitable for graphing.
        /// </summary>
        /// <param name="key">The metric key to query.</param>
        /// <param name="start">The start of the time range (inclusive).</param>
        /// <param name="end">The end of the time range (inclusive).</param>
        /// <param name="maxSamples">The maximum number of data points to return.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A list of sampled data points.</returns>
        Task<IReadOnlyList<MetricDataPoint>> GetRangeAsync(MetricKey key, DateTime start, DateTime end, int maxSamples, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the change between the snapshots closest to two specified dates for a metric.
        /// </summary>
        /// <param name="key">The metric key to query.</param>
        /// <param name="currentDate">The target date for the current (more recent) snapshot.</param>
        /// <param name="previousDate">The target date for the previous (older) snapshot.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>The calculated delta between the two snapshots.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when either the current or previous snapshot cannot be found.
        /// </exception>
        Task<MetricDelta> GetDeltaAsync(MetricKey key, DateTime currentDate, DateTime previousDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes a batch of snapshot, range, and delta requests over a shared database session.
        /// Individual request failures are captured as errors in the response rather than thrown.
        /// </summary>
        /// <param name="request">The batch query request containing all individual requests.</param>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>A response containing results and any errors that occurred.</returns>
        Task<MetricQueryResponse> QueryAsync(MetricQueryRequest request, CancellationToken cancellationToken = default);
    }
}
