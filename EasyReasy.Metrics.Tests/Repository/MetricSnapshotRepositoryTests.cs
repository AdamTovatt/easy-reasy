using System.Data.Common;
using EasyReasy.Database;
using EasyReasy.Metrics.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace EasyReasy.Metrics.Tests.Repository
{
    [TestClass]
    public class MetricSnapshotRepositoryTests
    {
        private readonly MetricSnapshotRepository _repository;

        public MetricSnapshotRepositoryTests()
        {
            Mock<DbDataSource> dataSourceMock = new Mock<DbDataSource>();
            Mock<IDbSessionFactory> sessionFactoryMock = new Mock<IDbSessionFactory>();
            _repository = new MetricSnapshotRepository(dataSourceMock.Object, sessionFactoryMock.Object);
        }

        [TestMethod]
        public async Task GetSampledRangeAsync_WithZeroMaxSamples_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime end = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                () => _repository.GetSampledRangeAsync(key, start, end, 0));
        }

        [TestMethod]
        public async Task GetSampledRangeAsync_WithNegativeMaxSamples_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime end = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                () => _repository.GetSampledRangeAsync(key, start, end, -5));
        }

        [TestMethod]
        public async Task GetSampledRangeAsync_WithEndBeforeStart_ThrowsArgumentException()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime start = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime end = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _repository.GetSampledRangeAsync(key, start, end, 10));
        }

        [TestMethod]
        public async Task GetSampledRangeAsync_WithEqualStartAndEnd_ThrowsArgumentException()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime sameDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => _repository.GetSampledRangeAsync(key, sameDate, sameDate, 10));
        }
    }
}
