using EasyReasy.Database;
using EasyReasy.Metrics.Models;
using EasyReasy.Metrics.Repository;

namespace EasyReasy.Metrics.Query
{
    /// <summary>
    /// Implementation of <see cref="IMetricQueryService"/> that delegates individual queries to the
    /// repository and processes batch queries over a shared database session.
    /// </summary>
    public class MetricQueryService : IMetricQueryService
    {
        private readonly IMetricSnapshotRepository _repository;
        private readonly IDbSessionFactory _sessionFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricQueryService"/> class.
        /// </summary>
        /// <param name="repository">The metric snapshot repository for data access.</param>
        /// <param name="sessionFactory">The session factory for creating shared database sessions.</param>
        public MetricQueryService(
            IMetricSnapshotRepository repository,
            IDbSessionFactory sessionFactory)
        {
            _repository = repository;
            _sessionFactory = sessionFactory;
        }

        /// <inheritdoc />
        public async Task<MetricSnapshot?> GetLatestAsync(MetricKey key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _repository.GetLatestAsync(key);
        }

        /// <inheritdoc />
        public async Task<MetricSnapshot?> GetClosestAsync(MetricKey key, DateTime targetDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _repository.GetClosestAsync(key, targetDate);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MetricDataPoint>> GetRangeAsync(MetricKey key, DateTime start, DateTime end, int maxSamples, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _repository.GetSampledRangeAsync(key, start, end, maxSamples);
        }

        /// <inheritdoc />
        public async Task<MetricDelta> GetDeltaAsync(MetricKey key, DateTime currentDate, DateTime previousDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MetricSnapshot? currentSnapshot = await _repository.GetClosestAsync(key, currentDate);
            if (currentSnapshot is null)
            {
                throw new InvalidOperationException(
                    $"No snapshot found for metric '{key}' near the current date {currentDate:O}.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            MetricSnapshot? previousSnapshot = await _repository.GetClosestAsync(key, previousDate);
            if (previousSnapshot is null)
            {
                throw new InvalidOperationException(
                    $"No snapshot found for metric '{key}' near the previous date {previousDate:O}.");
            }

            return BuildDelta(key, currentSnapshot, previousSnapshot);
        }

        /// <inheritdoc />
        public async Task<MetricQueryResponse> QueryAsync(MetricQueryRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MetricQueryResponse response = new MetricQueryResponse();

            await using IDbSession session = await _sessionFactory.CreateSessionAsync();

            foreach (SnapshotRequest snapshotRequest in request.SnapshotRequests)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string keyString = snapshotRequest.MetricKey.Key;

                if (response.Snapshots.ContainsKey(keyString))
                {
                    response.Errors.Add(
                        $"Duplicate snapshot request for metric '{snapshotRequest.MetricKey}'. Only the first request is processed.");
                    continue;
                }

                try
                {
                    MetricSnapshot? snapshot = await _repository.GetLatestAsync(snapshotRequest.MetricKey, session);
                    response.Snapshots[keyString] = snapshot;
                }
                catch (Exception exception)
                {
                    response.Errors.Add(
                        $"Failed to get snapshot for '{snapshotRequest.MetricKey}': {exception.Message}");
                }
            }

            foreach (RangeRequest rangeRequest in request.RangeRequests)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string keyString = rangeRequest.MetricKey.Key;

                if (response.Ranges.ContainsKey(keyString))
                {
                    response.Errors.Add(
                        $"Duplicate range request for metric '{rangeRequest.MetricKey}'. Only the first request is processed.");
                    continue;
                }

                try
                {
                    IReadOnlyList<MetricDataPoint> dataPoints = await _repository.GetSampledRangeAsync(
                        rangeRequest.MetricKey,
                        rangeRequest.Start,
                        rangeRequest.End,
                        rangeRequest.MaxSamples,
                        session);
                    response.Ranges[keyString] = dataPoints;
                }
                catch (Exception exception)
                {
                    response.Errors.Add(
                        $"Failed to get range for '{rangeRequest.MetricKey}': {exception.Message}");
                }
            }

            foreach (DeltaRequest deltaRequest in request.DeltaRequests)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string keyString = deltaRequest.MetricKey.Key;

                if (response.Deltas.ContainsKey(keyString))
                {
                    response.Errors.Add(
                        $"Duplicate delta request for metric '{deltaRequest.MetricKey}'. Only the first request is processed.");
                    continue;
                }

                try
                {
                    MetricSnapshot? currentSnapshot = await _repository.GetClosestAsync(
                        deltaRequest.MetricKey, deltaRequest.CurrentDate, session);

                    if (currentSnapshot is null)
                    {
                        throw new InvalidOperationException(
                            $"No snapshot found for metric '{deltaRequest.MetricKey}' near the current date {deltaRequest.CurrentDate:O}.");
                    }

                    MetricSnapshot? previousSnapshot = await _repository.GetClosestAsync(
                        deltaRequest.MetricKey, deltaRequest.PreviousDate, session);

                    if (previousSnapshot is null)
                    {
                        throw new InvalidOperationException(
                            $"No snapshot found for metric '{deltaRequest.MetricKey}' near the previous date {deltaRequest.PreviousDate:O}.");
                    }

                    response.Deltas[keyString] = BuildDelta(deltaRequest.MetricKey, currentSnapshot, previousSnapshot);
                }
                catch (Exception exception)
                {
                    response.Errors.Add(
                        $"Failed to get delta for '{deltaRequest.MetricKey}': {exception.Message}");
                }
            }

            return response;
        }

        /// <summary>
        /// Builds a <see cref="MetricDelta"/> from two snapshots, calculating the absolute and percentage change.
        /// </summary>
        /// <param name="key">The metric key the delta is for.</param>
        /// <param name="current">The current (more recent) snapshot.</param>
        /// <param name="previous">The previous (older) snapshot.</param>
        /// <returns>A <see cref="MetricDelta"/> with the calculated change values.</returns>
        private static MetricDelta BuildDelta(MetricKey key, MetricSnapshot current, MetricSnapshot previous)
        {
            decimal change = current.Value - previous.Value;
            decimal? percentageChange = previous.Value != 0
                ? (change / previous.Value) * 100
                : null;

            return new MetricDelta
            {
                MetricKey = key,
                Current = current,
                Previous = previous,
                Change = change,
                PercentageChange = percentageChange
            };
        }
    }
}
