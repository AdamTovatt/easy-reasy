using EasyReasy.Metrics.Collection;
using EasyReasy.Metrics.Query;
using EasyReasy.Metrics.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EasyReasy.Metrics.Tests
{
    [TestClass]
    public class MetricsServiceCollectionExtensionsTests
    {
        [TestMethod]
        public void AddEasyReasyMetrics_RegistersAllServices()
        {
            // Arrange
            ServiceCollection services = new ServiceCollection();

            // Act
            services.AddEasyReasyMetrics();

            // Assert
            Assert.IsTrue(HasRegistration<IMetricSnapshotRepository>(services));
            Assert.IsTrue(HasRegistration<IMetricCollectionService>(services));
            Assert.IsTrue(HasRegistration<IMetricQueryService>(services));
            Assert.IsTrue(HasRegistration<IHostedService>(services));
        }

        [TestMethod]
        public void AddEasyReasyMetrics_WithInvalidOptions_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            ServiceCollection services = new ServiceCollection();

            // Act & Assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                services.AddEasyReasyMetrics(options =>
                {
                    options.CollectionInterval = TimeSpan.Zero;
                }));
        }

        [TestMethod]
        public void AddMetricCollector_RegistersAsIMetricCollector()
        {
            // Arrange
            ServiceCollection services = new ServiceCollection();

            // Act
            services.AddMetricCollector<TestCollector>();

            // Assert
            bool hasCollectorRegistration = services.Any(
                descriptor => descriptor.ServiceType == typeof(IMetricCollector)
                    && descriptor.ImplementationType == typeof(TestCollector)
                    && descriptor.Lifetime == ServiceLifetime.Scoped);

            Assert.IsTrue(hasCollectorRegistration);
        }

        private static bool HasRegistration<TService>(ServiceCollection services)
        {
            return services.Any(descriptor => descriptor.ServiceType == typeof(TService));
        }

        private class TestCollector : IMetricCollector
        {
            public MetricKey MetricKey => new MetricKey("test_metric");

            public Task<decimal> CollectAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(0m);
            }
        }
    }
}
