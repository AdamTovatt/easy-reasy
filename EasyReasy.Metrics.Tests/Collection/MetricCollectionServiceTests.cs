using EasyReasy.Metrics.Collection;
using EasyReasy.Metrics.Configuration;
using EasyReasy.Metrics.Models;
using EasyReasy.Metrics.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace EasyReasy.Metrics.Tests.Collection
{
    [TestClass]
    public class MetricCollectionServiceTests
    {
        private readonly Mock<IMetricSnapshotRepository> _repositoryMock = new Mock<IMetricSnapshotRepository>();
        private readonly ILogger<MetricCollectionService> _logger = NullLogger<MetricCollectionService>.Instance;
        private readonly IOptions<EasyReasyMetricsOptions> _options = Options.Create(new EasyReasyMetricsOptions
        {
            MinimumTimeBetweenCollections = TimeSpan.FromMinutes(30)
        });

        [TestMethod]
        public async Task CollectAllMetricsAsync_WithMultipleCollectors_CollectsAll()
        {
            // Arrange
            MetricKey keyA = new MetricKey("metric_a");
            MetricKey keyB = new MetricKey("metric_b");

            TestCollector collectorA = new TestCollector(keyA, 42.5m);
            TestCollector collectorB = new TestCollector(keyB, 100.0m);

            _repositoryMock
                .Setup(r => r.GetLastCollectionTimeAsync(It.IsAny<MetricKey>(), null))
                .ReturnsAsync((DateTime?)null);

            _repositoryMock
                .Setup(r => r.InsertAsync(It.IsAny<MetricKey>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), null))
                .ReturnsAsync(1L);

            List<IMetricCollector> collectors = new List<IMetricCollector> { collectorA, collectorB };
            MetricCollectionService service = new MetricCollectionService(collectors, _repositoryMock.Object, _logger, _options);

            // Act
            await service.CollectAllMetricsAsync();

            // Assert
            _repositoryMock.Verify(
                r => r.InsertAsync(keyA, It.IsAny<DateTime>(), 42.5m, null),
                Times.Once);
            _repositoryMock.Verify(
                r => r.InsertAsync(keyB, It.IsAny<DateTime>(), 100.0m, null),
                Times.Once);
        }

        [TestMethod]
        public async Task CollectAllMetricsAsync_WhenCollectorThrows_ContinuesWithOthers()
        {
            // Arrange
            MetricKey keyA = new MetricKey("metric_a");
            MetricKey keyB = new MetricKey("metric_b");

            FailingCollector failingCollector = new FailingCollector(keyA);
            TestCollector goodCollector = new TestCollector(keyB, 99.0m);

            _repositoryMock
                .Setup(r => r.GetLastCollectionTimeAsync(It.IsAny<MetricKey>(), null))
                .ReturnsAsync((DateTime?)null);

            _repositoryMock
                .Setup(r => r.InsertAsync(It.IsAny<MetricKey>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), null))
                .ReturnsAsync(1L);

            List<IMetricCollector> collectors = new List<IMetricCollector> { failingCollector, goodCollector };
            MetricCollectionService service = new MetricCollectionService(collectors, _repositoryMock.Object, _logger, _options);

            // Act
            await service.CollectAllMetricsAsync();

            // Assert
            _repositoryMock.Verify(
                r => r.InsertAsync(keyB, It.IsAny<DateTime>(), 99.0m, null),
                Times.Once);
            _repositoryMock.Verify(
                r => r.InsertAsync(keyA, It.IsAny<DateTime>(), It.IsAny<decimal>(), null),
                Times.Never);
        }

        [TestMethod]
        public async Task CollectAllMetricsAsync_WhenWithinDeduplicationWindow_SkipsCollection()
        {
            // Arrange
            MetricKey key = new MetricKey("metric_a");
            TestCollector collector = new TestCollector(key, 42.5m);

            DateTime recentTime = DateTime.UtcNow.AddMinutes(-10); // Within the 30-minute window
            _repositoryMock
                .Setup(r => r.GetLastCollectionTimeAsync(key, null))
                .ReturnsAsync(recentTime);

            List<IMetricCollector> collectors = new List<IMetricCollector> { collector };
            MetricCollectionService service = new MetricCollectionService(collectors, _repositoryMock.Object, _logger, _options);

            // Act
            await service.CollectAllMetricsAsync();

            // Assert
            _repositoryMock.Verify(
                r => r.InsertAsync(It.IsAny<MetricKey>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), null),
                Times.Never);
        }

        [TestMethod]
        public async Task CollectAllMetricsAsync_WhenOutsideDeduplicationWindow_Collects()
        {
            // Arrange
            MetricKey key = new MetricKey("metric_a");
            TestCollector collector = new TestCollector(key, 42.5m);

            DateTime oldTime = DateTime.UtcNow.AddHours(-2); // Outside the 30-minute window
            _repositoryMock
                .Setup(r => r.GetLastCollectionTimeAsync(key, null))
                .ReturnsAsync(oldTime);

            _repositoryMock
                .Setup(r => r.InsertAsync(It.IsAny<MetricKey>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), null))
                .ReturnsAsync(1L);

            List<IMetricCollector> collectors = new List<IMetricCollector> { collector };
            MetricCollectionService service = new MetricCollectionService(collectors, _repositoryMock.Object, _logger, _options);

            // Act
            await service.CollectAllMetricsAsync();

            // Assert
            _repositoryMock.Verify(
                r => r.InsertAsync(key, It.IsAny<DateTime>(), 42.5m, null),
                Times.Once);
        }

        [TestMethod]
        public async Task CollectAllMetricsAsync_WhenNoPreviousSnapshot_Collects()
        {
            // Arrange
            MetricKey key = new MetricKey("metric_a");
            TestCollector collector = new TestCollector(key, 42.5m);

            _repositoryMock
                .Setup(r => r.GetLastCollectionTimeAsync(key, null))
                .ReturnsAsync((DateTime?)null);

            _repositoryMock
                .Setup(r => r.InsertAsync(It.IsAny<MetricKey>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), null))
                .ReturnsAsync(1L);

            List<IMetricCollector> collectors = new List<IMetricCollector> { collector };
            MetricCollectionService service = new MetricCollectionService(collectors, _repositoryMock.Object, _logger, _options);

            // Act
            await service.CollectAllMetricsAsync();

            // Assert
            _repositoryMock.Verify(
                r => r.InsertAsync(key, It.IsAny<DateTime>(), 42.5m, null),
                Times.Once);
        }

        [TestMethod]
        public async Task CollectAllMetricsAsync_WithNoCollectors_DoesNotThrow()
        {
            // Arrange
            List<IMetricCollector> collectors = new List<IMetricCollector>();
            MetricCollectionService service = new MetricCollectionService(collectors, _repositoryMock.Object, _logger, _options);

            // Act
            await service.CollectAllMetricsAsync();

            // Assert
            _repositoryMock.Verify(
                r => r.InsertAsync(It.IsAny<MetricKey>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), null),
                Times.Never);
        }

        [TestMethod]
        public async Task CollectAllMetricsAsync_WhenCancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            MetricKey key = new MetricKey("metric_a");

            _repositoryMock
                .Setup(r => r.GetLastCollectionTimeAsync(key, null))
                .ReturnsAsync((DateTime?)null);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            CancellingCollector collector = new CancellingCollector(key, cancellationTokenSource.Token);

            List<IMetricCollector> collectors = new List<IMetricCollector> { collector };
            MetricCollectionService service = new MetricCollectionService(collectors, _repositoryMock.Object, _logger, _options);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => service.CollectAllMetricsAsync(cancellationTokenSource.Token));
        }

        [TestMethod]
        public async Task CollectAllMetricsAsync_WhenInsertFails_LogsAndContinues()
        {
            // Arrange
            MetricKey keyA = new MetricKey("metric_a");
            MetricKey keyB = new MetricKey("metric_b");

            TestCollector collectorA = new TestCollector(keyA, 42.5m);
            TestCollector collectorB = new TestCollector(keyB, 99.0m);

            _repositoryMock
                .Setup(r => r.GetLastCollectionTimeAsync(It.IsAny<MetricKey>(), null))
                .ReturnsAsync((DateTime?)null);

            _repositoryMock
                .Setup(r => r.InsertAsync(keyA, It.IsAny<DateTime>(), It.IsAny<decimal>(), null))
                .ThrowsAsync(new InvalidOperationException("Database write failed"));

            _repositoryMock
                .Setup(r => r.InsertAsync(keyB, It.IsAny<DateTime>(), It.IsAny<decimal>(), null))
                .ReturnsAsync(2L);

            List<IMetricCollector> collectors = new List<IMetricCollector> { collectorA, collectorB };
            MetricCollectionService service = new MetricCollectionService(collectors, _repositoryMock.Object, _logger, _options);

            // Act
            await service.CollectAllMetricsAsync();

            // Assert — second collector still runs despite first insert failing
            _repositoryMock.Verify(
                r => r.InsertAsync(keyB, It.IsAny<DateTime>(), 99.0m, null),
                Times.Once);
        }

        private class TestCollector : IMetricCollector
        {
            private readonly decimal _value;

            public MetricKey MetricKey { get; }

            public TestCollector(MetricKey metricKey, decimal value)
            {
                MetricKey = metricKey;
                _value = value;
            }

            public Task<decimal> CollectAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_value);
            }
        }

        private class CancellingCollector : IMetricCollector
        {
            private readonly CancellationToken _cancellationToken;

            public MetricKey MetricKey { get; }

            public CancellingCollector(MetricKey metricKey, CancellationToken cancellationToken)
            {
                MetricKey = metricKey;
                _cancellationToken = cancellationToken;
            }

            public Task<decimal> CollectAsync(CancellationToken cancellationToken)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private class FailingCollector : IMetricCollector
        {
            public MetricKey MetricKey { get; }

            public FailingCollector(MetricKey metricKey)
            {
                MetricKey = metricKey;
            }

            public Task<decimal> CollectAsync(CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Collection failed");
            }
        }
    }
}
