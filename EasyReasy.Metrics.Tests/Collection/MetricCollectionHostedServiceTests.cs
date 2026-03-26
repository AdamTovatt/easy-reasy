using EasyReasy.Metrics.Collection;
using EasyReasy.Metrics.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EasyReasy.Metrics.Tests.Collection
{
    [TestClass]
    public class MetricCollectionHostedServiceTests
    {
        [TestMethod]
        public async Task StartAsync_ValidatesDuplicateMetricKeys_ThrowsOnDuplicate()
        {
            // Arrange
            ServiceCollection services = new ServiceCollection();
            services.AddScoped<IMetricCollector, DuplicateCollectorA>();
            services.AddScoped<IMetricCollector, DuplicateCollectorB>();
            services.AddScoped<IMetricCollectionService, NoOpMetricCollectionService>();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            IOptions<EasyReasyMetricsOptions> options = Options.Create(new EasyReasyMetricsOptions
            {
                InitialDelay = TimeSpan.Zero,
                CollectionInterval = TimeSpan.FromHours(1)
            });

            MetricCollectionHostedService hostedService = new MetricCollectionHostedService(
                scopeFactory,
                options,
                NullLogger<MetricCollectionHostedService>.Instance);

            // Act & Assert — StartAsync validates immediately before starting the background loop
            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => hostedService.StartAsync(CancellationToken.None));

            Assert.IsTrue(exception.Message.Contains("Duplicate metric keys detected"));
            Assert.IsTrue(exception.Message.Contains("duplicate_key"));
        }

        [TestMethod]
        public async Task StartAsync_WithNoCollectors_Succeeds()
        {
            // Arrange
            ServiceCollection services = new ServiceCollection();
            services.AddScoped<IMetricCollectionService, NoOpMetricCollectionService>();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            IOptions<EasyReasyMetricsOptions> options = Options.Create(new EasyReasyMetricsOptions
            {
                InitialDelay = TimeSpan.Zero,
                CollectionInterval = TimeSpan.FromHours(1)
            });

            MetricCollectionHostedService hostedService = new MetricCollectionHostedService(
                scopeFactory,
                options,
                NullLogger<MetricCollectionHostedService>.Instance);

            // Act & Assert — should not throw
            await hostedService.StartAsync(CancellationToken.None);
            await hostedService.StopAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task StartAsync_WithUniqueCollectors_Succeeds()
        {
            // Arrange
            ServiceCollection services = new ServiceCollection();
            services.AddScoped<IMetricCollector, DuplicateCollectorA>(); // "duplicate_key" name is misleading here, but it's a unique registration
            services.AddScoped<IMetricCollectionService, NoOpMetricCollectionService>();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceScopeFactory scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            IOptions<EasyReasyMetricsOptions> options = Options.Create(new EasyReasyMetricsOptions
            {
                InitialDelay = TimeSpan.Zero,
                CollectionInterval = TimeSpan.FromHours(1)
            });

            MetricCollectionHostedService hostedService = new MetricCollectionHostedService(
                scopeFactory,
                options,
                NullLogger<MetricCollectionHostedService>.Instance);

            // Act & Assert — single collector, no duplicates, should not throw
            await hostedService.StartAsync(CancellationToken.None);
            await hostedService.StopAsync(CancellationToken.None);
        }

        private class DuplicateCollectorA : IMetricCollector
        {
            public MetricKey MetricKey => new MetricKey("duplicate_key");

            public Task<decimal> CollectAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(0m);
            }
        }

        private class DuplicateCollectorB : IMetricCollector
        {
            public MetricKey MetricKey => new MetricKey("duplicate_key");

            public Task<decimal> CollectAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(0m);
            }
        }

        private class NoOpMetricCollectionService : IMetricCollectionService
        {
            public Task CollectAllMetricsAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}
