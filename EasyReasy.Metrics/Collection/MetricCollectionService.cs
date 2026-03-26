using EasyReasy.Metrics.Configuration;
using EasyReasy.Metrics.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyReasy.Metrics.Collection
{
    /// <summary>
    /// Implementation of <see cref="IMetricCollectionService"/> that iterates through all registered
    /// collectors, calls <see cref="IMetricCollector.CollectAsync"/> on each, and stores the results
    /// via the repository. Provides error isolation and deduplication.
    /// </summary>
    public class MetricCollectionService : IMetricCollectionService
    {
        private readonly IEnumerable<IMetricCollector> _collectors;
        private readonly IMetricSnapshotRepository _repository;
        private readonly ILogger<MetricCollectionService> _logger;
        private readonly EasyReasyMetricsOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricCollectionService"/> class.
        /// </summary>
        /// <param name="collectors">The registered metric collectors.</param>
        /// <param name="repository">The repository for storing metric snapshots.</param>
        /// <param name="logger">The logger for recording collection activity and errors.</param>
        /// <param name="options">The metrics configuration options.</param>
        public MetricCollectionService(
            IEnumerable<IMetricCollector> collectors,
            IMetricSnapshotRepository repository,
            ILogger<MetricCollectionService> logger,
            IOptions<EasyReasyMetricsOptions> options)
        {
            _collectors = collectors;
            _repository = repository;
            _logger = logger;
            _options = options.Value;
        }

        /// <inheritdoc />
        public async Task CollectAllMetricsAsync(CancellationToken cancellationToken = default)
        {
            foreach (IMetricCollector collector in _collectors)
            {
                try
                {
                    DateTime? lastCollectionTime = await _repository.GetLastCollectionTimeAsync(collector.MetricKey);

                    if (lastCollectionTime.HasValue)
                    {
                        TimeSpan timeSinceLastCollection = DateTime.UtcNow - lastCollectionTime.Value;

                        if (timeSinceLastCollection < _options.MinimumTimeBetweenCollections)
                        {
                            _logger.LogDebug(
                                "Skipping collection for metric '{MetricKey}' — last collected {TimeSince} ago, " +
                                "minimum interval is {MinInterval}",
                                collector.MetricKey,
                                timeSinceLastCollection,
                                _options.MinimumTimeBetweenCollections);
                            continue;
                        }
                    }

                    decimal value = await collector.CollectAsync(cancellationToken);
                    DateTime collectedAt = DateTime.UtcNow;

                    await _repository.InsertAsync(collector.MetricKey, collectedAt, value);

                    _logger.LogInformation(
                        "Collected metric '{MetricKey}' with value {Value} at {CollectedAt}",
                        collector.MetricKey,
                        value,
                        collectedAt);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Failed to collect metric '{MetricKey}'",
                        collector.MetricKey);
                }
            }
        }
    }
}
