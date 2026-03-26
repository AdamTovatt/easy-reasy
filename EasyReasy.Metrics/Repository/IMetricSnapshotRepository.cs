using EasyReasy.Database;
using EasyReasy.Metrics.Models;

namespace EasyReasy.Metrics.Repository
{
    /// <summary>
    /// Repository interface for metric snapshot data access operations on the <c>metric_snapshot</c> table.
    /// Extends <see cref="IRepository"/> from EasyReasy.Database for consistent session management.
    /// </summary>
    public interface IMetricSnapshotRepository : IRepository
    {
        /// <summary>
        /// Inserts a new metric snapshot into the database.
        /// </summary>
        /// <param name="key">The metric key identifying which metric this snapshot belongs to.</param>
        /// <param name="collectedAt">The UTC timestamp when the metric value was collected.</param>
        /// <param name="value">The numeric value of the metric.</param>
        /// <param name="session">Optional database session for transactional operations.</param>
        /// <returns>The database-generated identifier of the inserted snapshot.</returns>
        Task<long> InsertAsync(MetricKey key, DateTime collectedAt, decimal value, IDbSession? session = null);

        /// <summary>
        /// Gets the most recent snapshot for the specified metric key.
        /// </summary>
        /// <param name="key">The metric key to query.</param>
        /// <param name="session">Optional database session for transactional operations.</param>
        /// <returns>The latest snapshot, or <c>null</c> if no snapshots exist for this key.</returns>
        Task<MetricSnapshot?> GetLatestAsync(MetricKey key, IDbSession? session = null);

        /// <summary>
        /// Gets the snapshot closest to the specified target date for the given metric key.
        /// Uses a single query with <c>ORDER BY ABS(EXTRACT(EPOCH FROM (collected_at - targetDate)))</c>.
        /// </summary>
        /// <param name="key">The metric key to query.</param>
        /// <param name="targetDate">The target date to find the closest snapshot to.</param>
        /// <param name="session">Optional database session for transactional operations.</param>
        /// <returns>The closest snapshot, or <c>null</c> if no snapshots exist for this key.</returns>
        Task<MetricSnapshot?> GetClosestAsync(MetricKey key, DateTime targetDate, IDbSession? session = null);

        /// <summary>
        /// Gets sampled data points within a time range for the specified metric, using the
        /// LATERAL JOIN bucket approach for even distribution across the range.
        /// </summary>
        /// <param name="key">The metric key to query.</param>
        /// <param name="start">The start of the time range (inclusive).</param>
        /// <param name="end">The end of the time range (inclusive).</param>
        /// <param name="maxSamples">The maximum number of sampled data points to return.</param>
        /// <param name="session">Optional database session for transactional operations.</param>
        /// <returns>A list of sampled data points.</returns>
        Task<IReadOnlyList<MetricDataPoint>> GetSampledRangeAsync(MetricKey key, DateTime start, DateTime end, int maxSamples, IDbSession? session = null);

        /// <summary>
        /// Atomically inserts a new metric snapshot only if no snapshot for the same metric key
        /// was collected within the specified minimum interval. Prevents duplicate collections
        /// when multiple processes race to collect the same metric.
        /// </summary>
        /// <param name="key">The metric key identifying which metric this snapshot belongs to.</param>
        /// <param name="collectedAt">The UTC timestamp when the metric value was collected.</param>
        /// <param name="value">The numeric value of the metric.</param>
        /// <param name="minimumInterval">The minimum time that must have elapsed since the last collection.</param>
        /// <param name="session">Optional database session for transactional operations.</param>
        /// <returns>The database-generated identifier of the inserted snapshot, or <c>null</c> if the insert was skipped due to a recent collection.</returns>
        Task<long?> InsertIfNotRecentAsync(MetricKey key, DateTime collectedAt, decimal value, TimeSpan minimumInterval, IDbSession? session = null);

        /// <summary>
        /// Gets the most recent collection timestamp for the specified metric key.
        /// Used for deduplication to avoid collecting the same metric too frequently.
        /// </summary>
        /// <param name="key">The metric key to query.</param>
        /// <param name="session">Optional database session for transactional operations.</param>
        /// <returns>The timestamp of the most recent collection, or <c>null</c> if no snapshots exist.</returns>
        Task<DateTime?> GetLastCollectionTimeAsync(MetricKey key, IDbSession? session = null);

        /// <summary>
        /// Creates the <c>metric_snapshot</c> table and its index if they do not already exist.
        /// Safe to call multiple times (idempotent). Useful for integration tests and quick prototyping.
        /// </summary>
        /// <param name="session">Optional database session for transactional operations.</param>
        Task EnsureSchemaAsync(IDbSession? session = null);
    }
}
