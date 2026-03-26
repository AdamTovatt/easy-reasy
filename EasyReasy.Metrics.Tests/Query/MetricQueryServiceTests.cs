using EasyReasy.Database;
using EasyReasy.Metrics.Models;
using EasyReasy.Metrics.Query;
using EasyReasy.Metrics.Repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace EasyReasy.Metrics.Tests.Query
{
    [TestClass]
    public class MetricQueryServiceTests
    {
        private readonly Mock<IMetricSnapshotRepository> _repositoryMock;
        private readonly Mock<IDbSessionFactory> _sessionFactoryMock;
        private readonly Mock<IDbSession> _sessionMock;
        private readonly MetricQueryService _service;

        public MetricQueryServiceTests()
        {
            _repositoryMock = new Mock<IMetricSnapshotRepository>();
            _sessionFactoryMock = new Mock<IDbSessionFactory>();
            _sessionMock = new Mock<IDbSession>();

            _sessionFactoryMock
                .Setup(f => f.CreateSessionAsync())
                .ReturnsAsync(_sessionMock.Object);

            _service = new MetricQueryService(_repositoryMock.Object, _sessionFactoryMock.Object);
        }

        [TestMethod]
        public async Task GetLatestAsync_DelegatesToRepository()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            MetricSnapshot expectedSnapshot = new MetricSnapshot
            {
                Id = 1,
                MetricKey = key,
                CollectedAt = DateTime.UtcNow,
                Value = 150m
            };

            _repositoryMock
                .Setup(r => r.GetLatestAsync(key, null))
                .ReturnsAsync(expectedSnapshot);

            // Act
            MetricSnapshot? result = await _service.GetLatestAsync(key);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedSnapshot.Id, result.Id);
            Assert.AreEqual(expectedSnapshot.Value, result.Value);
            _repositoryMock.Verify(r => r.GetLatestAsync(key, null), Times.Once);
        }

        [TestMethod]
        public async Task GetClosestAsync_DelegatesToRepository()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime targetDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            MetricSnapshot expectedSnapshot = new MetricSnapshot
            {
                Id = 5,
                MetricKey = key,
                CollectedAt = new DateTime(2025, 6, 14, 23, 0, 0, DateTimeKind.Utc),
                Value = 200m
            };

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, targetDate, null))
                .ReturnsAsync(expectedSnapshot);

            // Act
            MetricSnapshot? result = await _service.GetClosestAsync(key, targetDate);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedSnapshot.Id, result.Id);
            _repositoryMock.Verify(r => r.GetClosestAsync(key, targetDate, null), Times.Once);
        }

        [TestMethod]
        public async Task GetRangeAsync_DelegatesToRepository()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime end = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            int maxSamples = 10;

            List<MetricDataPoint> expectedPoints = new List<MetricDataPoint>
            {
                new MetricDataPoint { CollectedAt = start, Value = 100m },
                new MetricDataPoint { CollectedAt = end, Value = 200m }
            };

            _repositoryMock
                .Setup(r => r.GetSampledRangeAsync(key, start, end, maxSamples, null))
                .ReturnsAsync(expectedPoints.AsReadOnly());

            // Act
            IReadOnlyList<MetricDataPoint> result = await _service.GetRangeAsync(key, start, end, maxSamples);

            // Assert
            Assert.AreEqual(2, result.Count);
            _repositoryMock.Verify(r => r.GetSampledRangeAsync(key, start, end, maxSamples, null), Times.Once);
        }

        [TestMethod]
        public async Task GetDeltaAsync_CalculatesChangeCorrectly()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime currentDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            MetricSnapshot currentSnapshot = new MetricSnapshot
            {
                Id = 10,
                MetricKey = key,
                CollectedAt = currentDate,
                Value = 200m
            };

            MetricSnapshot previousSnapshot = new MetricSnapshot
            {
                Id = 1,
                MetricKey = key,
                CollectedAt = previousDate,
                Value = 150m
            };

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, currentDate, null))
                .ReturnsAsync(currentSnapshot);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, previousDate, null))
                .ReturnsAsync(previousSnapshot);

            // Act
            MetricDelta result = await _service.GetDeltaAsync(key, currentDate, previousDate);

            // Assert
            Assert.AreEqual(50m, result.Change);
            Assert.AreEqual(key, result.MetricKey);
            Assert.AreEqual(currentSnapshot, result.Current);
            Assert.AreEqual(previousSnapshot, result.Previous);
        }

        [TestMethod]
        public async Task GetDeltaAsync_CalculatesPercentageChangeCorrectly()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime currentDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            MetricSnapshot currentSnapshot = new MetricSnapshot
            {
                Id = 10,
                MetricKey = key,
                CollectedAt = currentDate,
                Value = 200m
            };

            MetricSnapshot previousSnapshot = new MetricSnapshot
            {
                Id = 1,
                MetricKey = key,
                CollectedAt = previousDate,
                Value = 100m
            };

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, currentDate, null))
                .ReturnsAsync(currentSnapshot);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, previousDate, null))
                .ReturnsAsync(previousSnapshot);

            // Act
            MetricDelta result = await _service.GetDeltaAsync(key, currentDate, previousDate);

            // Assert
            Assert.AreEqual(100m, result.PercentageChange); // (200 - 100) / 100 * 100 = 100%
        }

        [TestMethod]
        public async Task GetDeltaAsync_WhenPreviousValueIsZero_PercentageChangeIsNull()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime currentDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            MetricSnapshot currentSnapshot = new MetricSnapshot
            {
                Id = 10,
                MetricKey = key,
                CollectedAt = currentDate,
                Value = 50m
            };

            MetricSnapshot previousSnapshot = new MetricSnapshot
            {
                Id = 1,
                MetricKey = key,
                CollectedAt = previousDate,
                Value = 0m
            };

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, currentDate, null))
                .ReturnsAsync(currentSnapshot);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, previousDate, null))
                .ReturnsAsync(previousSnapshot);

            // Act
            MetricDelta result = await _service.GetDeltaAsync(key, currentDate, previousDate);

            // Assert
            Assert.AreEqual(50m, result.Change);
            Assert.IsNull(result.PercentageChange);
        }

        [TestMethod]
        public async Task GetDeltaAsync_WhenCurrentSnapshotNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime currentDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, currentDate, null))
                .ReturnsAsync((MetricSnapshot?)null);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => _service.GetDeltaAsync(key, currentDate, previousDate));
        }

        [TestMethod]
        public async Task GetDeltaAsync_WhenPreviousSnapshotNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime currentDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            MetricSnapshot currentSnapshot = new MetricSnapshot
            {
                Id = 10,
                MetricKey = key,
                CollectedAt = currentDate,
                Value = 200m
            };

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, currentDate, null))
                .ReturnsAsync(currentSnapshot);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, previousDate, null))
                .ReturnsAsync((MetricSnapshot?)null);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => _service.GetDeltaAsync(key, currentDate, previousDate));
        }

        [TestMethod]
        public async Task QueryAsync_ProcessesSnapshotAndDeltaRequests()
        {
            // Arrange
            MetricKey snapshotKey = new MetricKey("total_customers");
            MetricKey deltaKey = new MetricKey("active_policies");
            DateTime currentDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            MetricSnapshot snapshotResult = new MetricSnapshot
            {
                Id = 1,
                MetricKey = snapshotKey,
                CollectedAt = DateTime.UtcNow,
                Value = 150m
            };

            MetricSnapshot deltaCurrentSnapshot = new MetricSnapshot
            {
                Id = 10,
                MetricKey = deltaKey,
                CollectedAt = currentDate,
                Value = 80m
            };

            MetricSnapshot deltaPreviousSnapshot = new MetricSnapshot
            {
                Id = 5,
                MetricKey = deltaKey,
                CollectedAt = previousDate,
                Value = 50m
            };

            _repositoryMock
                .Setup(r => r.GetLatestAsync(snapshotKey, _sessionMock.Object))
                .ReturnsAsync(snapshotResult);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(deltaKey, currentDate, _sessionMock.Object))
                .ReturnsAsync(deltaCurrentSnapshot);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(deltaKey, previousDate, _sessionMock.Object))
                .ReturnsAsync(deltaPreviousSnapshot);

            MetricQueryRequest request = new MetricQueryRequest
            {
                SnapshotRequests = new List<SnapshotRequest>
                {
                    new SnapshotRequest { MetricKey = snapshotKey }
                },
                DeltaRequests = new List<DeltaRequest>
                {
                    new DeltaRequest { MetricKey = deltaKey, CurrentDate = currentDate, PreviousDate = previousDate }
                }
            };

            // Act
            MetricQueryResponse response = await _service.QueryAsync(request);

            // Assert
            Assert.AreEqual(0, response.Errors.Count);
            Assert.IsTrue(response.Snapshots.ContainsKey(snapshotKey.Key));
            Assert.AreEqual(150m, response.Snapshots[snapshotKey.Key]!.Value);
            Assert.IsTrue(response.Deltas.ContainsKey(deltaKey.Key));
            Assert.AreEqual(30m, response.Deltas[deltaKey.Key].Change);
        }

        [TestMethod]
        public async Task QueryAsync_WhenRequestFails_CapturesErrorInResponse()
        {
            // Arrange
            MetricKey key = new MetricKey("failing_metric");

            _repositoryMock
                .Setup(r => r.GetLatestAsync(key, _sessionMock.Object))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            MetricQueryRequest request = new MetricQueryRequest
            {
                SnapshotRequests = new List<SnapshotRequest>
                {
                    new SnapshotRequest { MetricKey = key }
                }
            };

            // Act
            MetricQueryResponse response = await _service.QueryAsync(request);

            // Assert
            Assert.AreEqual(1, response.Errors.Count);
            Assert.IsTrue(response.Errors[0].Contains("Database error"));
        }

        [TestMethod]
        public async Task QueryAsync_WithRangeRequest_ReturnsDataPoints()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime end = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);

            List<MetricDataPoint> expectedPoints = new List<MetricDataPoint>
            {
                new MetricDataPoint { CollectedAt = start, Value = 100m },
                new MetricDataPoint { CollectedAt = end, Value = 200m }
            };

            _repositoryMock
                .Setup(r => r.GetSampledRangeAsync(key, start, end, 50, _sessionMock.Object))
                .ReturnsAsync(expectedPoints.AsReadOnly());

            MetricQueryRequest request = new MetricQueryRequest
            {
                RangeRequests = new List<RangeRequest>
                {
                    new RangeRequest { MetricKey = key, Start = start, End = end, MaxSamples = 50 }
                }
            };

            // Act
            MetricQueryResponse response = await _service.QueryAsync(request);

            // Assert
            Assert.AreEqual(0, response.Errors.Count);
            Assert.IsTrue(response.Ranges.ContainsKey(key.Key));
            Assert.AreEqual(2, response.Ranges[key.Key].Count);
        }

        [TestMethod]
        public async Task QueryAsync_WithDuplicateSnapshotRequests_CapturesDuplicateError()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            MetricSnapshot snapshot = new MetricSnapshot
            {
                Id = 1,
                MetricKey = key,
                CollectedAt = DateTime.UtcNow,
                Value = 150m
            };

            _repositoryMock
                .Setup(r => r.GetLatestAsync(key, _sessionMock.Object))
                .ReturnsAsync(snapshot);

            MetricQueryRequest request = new MetricQueryRequest
            {
                SnapshotRequests = new List<SnapshotRequest>
                {
                    new SnapshotRequest { MetricKey = key },
                    new SnapshotRequest { MetricKey = key }
                }
            };

            // Act
            MetricQueryResponse response = await _service.QueryAsync(request);

            // Assert
            Assert.AreEqual(1, response.Snapshots.Count);
            Assert.AreEqual(1, response.Errors.Count);
            Assert.IsTrue(response.Errors[0].Contains("Duplicate snapshot request"));
        }

        [TestMethod]
        public async Task QueryAsync_WithEmptyRequest_ReturnsEmptyResponse()
        {
            // Arrange
            MetricQueryRequest request = new MetricQueryRequest();

            // Act
            MetricQueryResponse response = await _service.QueryAsync(request);

            // Assert
            Assert.AreEqual(0, response.Snapshots.Count);
            Assert.AreEqual(0, response.Ranges.Count);
            Assert.AreEqual(0, response.Deltas.Count);
            Assert.AreEqual(0, response.Errors.Count);
        }

        [TestMethod]
        public async Task QueryAsync_DeltaWithMissingSnapshot_CapturesErrorInResponse()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime currentDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, currentDate, _sessionMock.Object))
                .ReturnsAsync((MetricSnapshot?)null);

            MetricQueryRequest request = new MetricQueryRequest
            {
                DeltaRequests = new List<DeltaRequest>
                {
                    new DeltaRequest { MetricKey = key, CurrentDate = currentDate, PreviousDate = previousDate }
                }
            };

            // Act
            MetricQueryResponse response = await _service.QueryAsync(request);

            // Assert — the error is captured, not thrown
            Assert.AreEqual(0, response.Deltas.Count);
            Assert.AreEqual(1, response.Errors.Count);
            Assert.IsTrue(response.Errors[0].Contains("No snapshot found"));
        }

        [TestMethod]
        public async Task GetDeltaAsync_NegativeChange_CalculatesCorrectly()
        {
            // Arrange
            MetricKey key = new MetricKey("total_customers");
            DateTime currentDate = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
            DateTime previousDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            MetricSnapshot currentSnapshot = new MetricSnapshot
            {
                Id = 10,
                MetricKey = key,
                CollectedAt = currentDate,
                Value = 80m
            };

            MetricSnapshot previousSnapshot = new MetricSnapshot
            {
                Id = 1,
                MetricKey = key,
                CollectedAt = previousDate,
                Value = 100m
            };

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, currentDate, null))
                .ReturnsAsync(currentSnapshot);

            _repositoryMock
                .Setup(r => r.GetClosestAsync(key, previousDate, null))
                .ReturnsAsync(previousSnapshot);

            // Act
            MetricDelta result = await _service.GetDeltaAsync(key, currentDate, previousDate);

            // Assert
            Assert.AreEqual(-20m, result.Change);
            Assert.AreEqual(-20m, result.PercentageChange);
        }
    }
}
