using EasyReasy.Metrics.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyReasy.Metrics.Collection
{
    /// <summary>
    /// A background service that periodically triggers metric collection on a configurable interval.
    /// Creates a new DI scope for each collection tick to properly resolve scoped services.
    /// Validates that no two collectors share the same <see cref="MetricKey"/> at startup.
    /// </summary>
    public class MetricCollectionHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly EasyReasyMetricsOptions _options;
        private readonly ILogger<MetricCollectionHostedService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricCollectionHostedService"/> class.
        /// </summary>
        /// <param name="scopeFactory">The service scope factory for creating scoped service providers.</param>
        /// <param name="options">The metrics configuration options.</param>
        /// <param name="logger">The logger for recording service activity and errors.</param>
        public MetricCollectionHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<EasyReasyMetricsOptions> options,
            ILogger<MetricCollectionHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ValidateDuplicateMetricKeys(scope.ServiceProvider);

            await base.StartAsync(cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(_options.InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using IServiceScope scope = _scopeFactory.CreateScope();

                    IMetricCollectionService collectionService =
                        scope.ServiceProvider.GetRequiredService<IMetricCollectionService>();

                    await collectionService.CollectAllMetricsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Metric collection failed");
                }

                await Task.Delay(_options.CollectionInterval, stoppingToken);
            }
        }

        /// <summary>
        /// Validates that no two registered collectors share the same <see cref="MetricKey"/>.
        /// Throws an <see cref="InvalidOperationException"/> listing all duplicate keys if any are found.
        /// </summary>
        /// <param name="serviceProvider">The service provider to resolve collectors from.</param>
        /// <exception cref="InvalidOperationException">Thrown when duplicate metric keys are detected.</exception>
        private static void ValidateDuplicateMetricKeys(IServiceProvider serviceProvider)
        {
            IEnumerable<IMetricCollector> collectors =
                serviceProvider.GetServices<IMetricCollector>();

            List<MetricKey> duplicateKeys = collectors
                .GroupBy(c => c.MetricKey)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateKeys.Count > 0)
            {
                string duplicateList = string.Join(", ", duplicateKeys.Select(k => $"'{k}'"));
                throw new InvalidOperationException(
                    $"Duplicate metric keys detected: {duplicateList}. " +
                    "Each metric key must be registered by exactly one collector.");
            }
        }
    }
}
