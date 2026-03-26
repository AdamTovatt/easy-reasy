using System.Data.Common;
using EasyReasy.Database;
using EasyReasy.Database.Mapping;
using EasyReasy.Metrics.Models;

namespace EasyReasy.Metrics.Repository
{
    /// <summary>
    /// PostgreSQL implementation of <see cref="IMetricSnapshotRepository"/> for the <c>metric_snapshot</c> table.
    /// Uses EasyReasy.Database.Mapping for SQL query execution with automatic snake_case mapping.
    /// </summary>
    public class MetricSnapshotRepository : RepositoryBase, IMetricSnapshotRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetricSnapshotRepository"/> class.
        /// </summary>
        /// <param name="dataSource">The database data source.</param>
        /// <param name="sessionFactory">The session factory for creating database sessions.</param>
        public MetricSnapshotRepository(DbDataSource dataSource, IDbSessionFactory sessionFactory)
            : base(dataSource, sessionFactory)
        {
        }

        /// <inheritdoc />
        public async Task<long> InsertAsync(MetricKey key, DateTime collectedAt, decimal value, IDbSession? session = null)
        {
            return await UseSessionAsync(async (dbSession) =>
            {
                string metricKey = key.Key;

                string query = $@"
                    INSERT INTO metric_snapshot (metric_key, collected_at, value)
                    VALUES (@{nameof(metricKey)}, @{nameof(collectedAt)}, @{nameof(value)})
                    RETURNING id";

                long id = await dbSession.Connection.ExecuteScalarAsync<long>(
                    query,
                    new { metricKey, collectedAt, value },
                    transaction: dbSession.Transaction);

                return id;
            }, session);
        }

        /// <inheritdoc />
        public async Task<MetricSnapshot?> GetLatestAsync(MetricKey key, IDbSession? session = null)
        {
            return await UseSessionAsync(async (dbSession) =>
            {
                string metricKey = key.Key;

                string query = $@"
                    SELECT id, metric_key, collected_at, value
                    FROM metric_snapshot
                    WHERE metric_key = @{nameof(metricKey)}
                    ORDER BY collected_at DESC
                    LIMIT 1";

                MetricSnapshotEntity? entity = await dbSession.Connection.QueryFirstOrDefaultAsync<MetricSnapshotEntity>(
                    query,
                    new { metricKey },
                    transaction: dbSession.Transaction);

                return entity?.ToDomain();
            }, session);
        }

        /// <inheritdoc />
        public async Task<MetricSnapshot?> GetClosestAsync(MetricKey key, DateTime targetDate, IDbSession? session = null)
        {
            return await UseSessionAsync(async (dbSession) =>
            {
                string metricKey = key.Key;

                string query = $@"
                    SELECT id, metric_key, collected_at, value
                    FROM (
                        (SELECT id, metric_key, collected_at, value
                         FROM metric_snapshot
                         WHERE metric_key = @{nameof(metricKey)} AND collected_at <= @{nameof(targetDate)}
                         ORDER BY collected_at DESC
                         LIMIT 1)
                        UNION ALL
                        (SELECT id, metric_key, collected_at, value
                         FROM metric_snapshot
                         WHERE metric_key = @{nameof(metricKey)} AND collected_at > @{nameof(targetDate)}
                         ORDER BY collected_at ASC
                         LIMIT 1)
                    ) AS candidates
                    ORDER BY ABS(EXTRACT(EPOCH FROM (collected_at - @{nameof(targetDate)})))
                    LIMIT 1";

                MetricSnapshotEntity? entity = await dbSession.Connection.QueryFirstOrDefaultAsync<MetricSnapshotEntity>(
                    query,
                    new { metricKey, targetDate },
                    transaction: dbSession.Transaction);

                return entity?.ToDomain();
            }, session);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MetricDataPoint>> GetSampledRangeAsync(MetricKey key, DateTime start, DateTime end, int maxSamples, IDbSession? session = null)
        {
            if (maxSamples <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSamples), "Must be a positive integer.");
            }

            if (end <= start)
            {
                throw new ArgumentException("End must be after start.", nameof(end));
            }

            return await UseSessionAsync(async (dbSession) =>
            {
                string metricKey = key.Key;

                string query = $@"
                    WITH buckets AS (
                        SELECT bucket_start
                        FROM generate_series(
                            @{nameof(start)}::timestamptz,
                            @{nameof(end)}::timestamptz,
                            ((@{nameof(end)}::timestamptz - @{nameof(start)}::timestamptz) / @{nameof(maxSamples)})
                        ) AS bucket_start
                        LIMIT @{nameof(maxSamples)}
                    )
                    SELECT s.collected_at, s.value
                    FROM buckets b
                    CROSS JOIN LATERAL (
                        SELECT collected_at, value
                        FROM metric_snapshot
                        WHERE metric_key = @{nameof(metricKey)}
                          AND collected_at >= b.bucket_start
                          AND collected_at < b.bucket_start + ((@{nameof(end)}::timestamptz - @{nameof(start)}::timestamptz) / @{nameof(maxSamples)})
                        ORDER BY collected_at
                        LIMIT 1
                    ) s
                    ORDER BY s.collected_at";

                IEnumerable<MetricDataPoint> dataPoints = await dbSession.Connection.QueryAsync<MetricDataPoint>(
                    query,
                    new { metricKey, start, end, maxSamples },
                    transaction: dbSession.Transaction);

                return dataPoints.ToList().AsReadOnly();
            }, session);
        }

        /// <inheritdoc />
        public async Task<long?> InsertIfNotRecentAsync(MetricKey key, DateTime collectedAt, decimal value, TimeSpan minimumInterval, IDbSession? session = null)
        {
            return await UseSessionAsync(async (dbSession) =>
            {
                string metricKey = key.Key;
                DateTime threshold = collectedAt - minimumInterval;

                string query = $@"
                    INSERT INTO metric_snapshot (metric_key, collected_at, value)
                    SELECT @{nameof(metricKey)}, @{nameof(collectedAt)}, @{nameof(value)}
                    WHERE NOT EXISTS (
                        SELECT 1 FROM metric_snapshot
                        WHERE metric_key = @{nameof(metricKey)}
                        AND collected_at > @{nameof(threshold)}
                    )
                    RETURNING id";

                long? id = await dbSession.Connection.QueryFirstOrDefaultAsync<long?>(
                    query,
                    new { metricKey, collectedAt, value, threshold },
                    transaction: dbSession.Transaction);

                return id;
            }, session);
        }

        /// <inheritdoc />
        public async Task<DateTime?> GetLastCollectionTimeAsync(MetricKey key, IDbSession? session = null)
        {
            return await UseSessionAsync(async (dbSession) =>
            {
                string metricKey = key.Key;

                string query = $@"
                    SELECT collected_at
                    FROM metric_snapshot
                    WHERE metric_key = @{nameof(metricKey)}
                    ORDER BY collected_at DESC
                    LIMIT 1";

                DateTime? collectedAt = await dbSession.Connection.QueryFirstOrDefaultAsync<DateTime?>(
                    query,
                    new { metricKey },
                    transaction: dbSession.Transaction);

                return collectedAt;
            }, session);
        }

        /// <inheritdoc />
        public async Task EnsureSchemaAsync(IDbSession? session = null)
        {
            await UseSessionAsync(async (dbSession) =>
            {
                string createTable = @"
                    CREATE TABLE IF NOT EXISTS metric_snapshot (
                        id BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                        metric_key TEXT NOT NULL,
                        collected_at TIMESTAMPTZ NOT NULL,
                        value NUMERIC NOT NULL
                    )";

                await dbSession.Connection.ExecuteAsync(
                    createTable,
                    transaction: dbSession.Transaction);

                string createIndex = @"
                    CREATE INDEX IF NOT EXISTS idx_metric_snapshot_key_collected
                        ON metric_snapshot (metric_key, collected_at DESC)";

                await dbSession.Connection.ExecuteAsync(
                    createIndex,
                    transaction: dbSession.Transaction);
            }, session);
        }
    }
}
