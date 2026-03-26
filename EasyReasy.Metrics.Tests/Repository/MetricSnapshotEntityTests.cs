using EasyReasy.Metrics.Models;
using EasyReasy.Metrics.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EasyReasy.Metrics.Tests.Repository
{
    [TestClass]
    public class MetricSnapshotEntityTests
    {
        [TestMethod]
        public void ToDomain_MapsAllFieldsCorrectly()
        {
            // Arrange
            DateTime collectedAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            MetricSnapshotEntity entity = new MetricSnapshotEntity
            {
                Id = 42,
                MetricKey = "total_customers",
                CollectedAt = collectedAt,
                Value = 150.75m
            };

            // Act
            MetricSnapshot snapshot = entity.ToDomain();

            // Assert
            Assert.AreEqual(42L, snapshot.Id);
            Assert.AreEqual(new MetricKey("total_customers"), snapshot.MetricKey);
            Assert.AreEqual(collectedAt, snapshot.CollectedAt);
            Assert.AreEqual(150.75m, snapshot.Value);
        }

        [TestMethod]
        public void ToDomain_PreservesZeroValue()
        {
            // Arrange
            MetricSnapshotEntity entity = new MetricSnapshotEntity
            {
                Id = 1,
                MetricKey = "empty_metric",
                CollectedAt = DateTime.UtcNow,
                Value = 0m
            };

            // Act
            MetricSnapshot snapshot = entity.ToDomain();

            // Assert
            Assert.AreEqual(0m, snapshot.Value);
        }

        [TestMethod]
        public void ToDomain_PreservesNegativeValue()
        {
            // Arrange
            MetricSnapshotEntity entity = new MetricSnapshotEntity
            {
                Id = 1,
                MetricKey = "profit_loss",
                CollectedAt = DateTime.UtcNow,
                Value = -500.25m
            };

            // Act
            MetricSnapshot snapshot = entity.ToDomain();

            // Assert
            Assert.AreEqual(-500.25m, snapshot.Value);
        }
    }
}
