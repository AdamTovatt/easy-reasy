using System.Data.Common;
using EasyReasy.Database;
using EasyReasy.Metrics.Models;
using EasyReasy.Metrics.Repository;

namespace EasyReasy.Metrics.IntegrationTests
{
    /// <summary>
    /// Integration tests for <see cref="MetricSnapshotRepository"/> against a real PostgreSQL database.
    /// </summary>
    [Collection("Database")]
    public class MetricSnapshotRepositoryTests : IAsyncLifetime
    {
        private readonly TestDatabaseFixture _fixture;
        private DbConnection _connection = null!;
        private DbTransaction _transaction = null!;
        private MetricSnapshotRepository _repository = null!;
        private IDbSession _session = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricSnapshotRepositoryTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared database fixture.</param>
        public MetricSnapshotRepositoryTests(TestDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            _connection = await _fixture.DataSource.OpenConnectionAsync();
            _transaction = await _connection.BeginTransactionAsync();
            _repository = new MetricSnapshotRepository(_fixture.DataSource, _fixture.SessionFactory);
            _session = new DbSession(_connection, _transaction);
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            await _session.DisposeAsync();
        }

        #region InsertAsync

        /// <summary>
        /// Verifies that InsertAsync returns a valid database-generated identifier.
        /// </summary>
        [Fact]
        public async Task InsertAsync_WithValidData_ReturnsNewId()
        {
            MetricKey testKey = new MetricKey("test_metric");
            DateTime collectedAt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

            long id = await _repository.InsertAsync(testKey, collectedAt, 42.5m, _session);

            Assert.True(id > 0);
        }

        /// <summary>
        /// Verifies that consecutive inserts return incrementing identifiers.
        /// </summary>
        [Fact]
        public async Task InsertAsync_MultipleCalls_ReturnsIncrementingIds()
        {
            MetricKey testKey = new MetricKey("test_metric");
            DateTime collectedAt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

            long firstId = await _repository.InsertAsync(testKey, collectedAt, 10m, _session);
            long secondId = await _repository.InsertAsync(testKey, collectedAt.AddHours(1), 20m, _session);

            Assert.True(secondId > firstId);
        }

        #endregion

        #region GetLatestAsync

        /// <summary>
        /// Verifies that GetLatestAsync returns the most recently collected snapshot.
        /// </summary>
        [Fact]
        public async Task GetLatestAsync_WithExistingData_ReturnsMostRecent()
        {
            MetricKey testKey = new MetricKey("latest_metric");
            DateTime olderTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            DateTime newerTime = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);

            await _repository.InsertAsync(testKey, olderTime, 100m, _session);
            await _repository.InsertAsync(testKey, newerTime, 200m, _session);

            MetricSnapshot? result = await _repository.GetLatestAsync(testKey, _session);

            Assert.NotNull(result);
            Assert.Equal(200m, result.Value);
            Assert.Equal(newerTime, result.CollectedAt.ToUniversalTime());
        }

        /// <summary>
        /// Verifies that GetLatestAsync returns null when no data exists for the key.
        /// </summary>
        [Fact]
        public async Task GetLatestAsync_WithNoData_ReturnsNull()
        {
            MetricKey testKey = new MetricKey("nonexistent_metric");

            MetricSnapshot? result = await _repository.GetLatestAsync(testKey, _session);

            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that GetLatestAsync returns the correct snapshot when multiple keys exist.
        /// </summary>
        [Fact]
        public async Task GetLatestAsync_WithMultipleKeys_ReturnsCorrectKey()
        {
            MetricKey keyA = new MetricKey("metric_a");
            MetricKey keyB = new MetricKey("metric_b");
            DateTime collectedAt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

            await _repository.InsertAsync(keyA, collectedAt, 111m, _session);
            await _repository.InsertAsync(keyB, collectedAt, 222m, _session);

            MetricSnapshot? resultA = await _repository.GetLatestAsync(keyA, _session);
            MetricSnapshot? resultB = await _repository.GetLatestAsync(keyB, _session);

            Assert.NotNull(resultA);
            Assert.NotNull(resultB);
            Assert.Equal(111m, resultA.Value);
            Assert.Equal(222m, resultB.Value);
            Assert.Equal(keyA, resultA.MetricKey);
            Assert.Equal(keyB, resultB.MetricKey);
        }

        #endregion

        #region GetClosestAsync

        /// <summary>
        /// Verifies that GetClosestAsync returns the snapshot closest to the target date.
        /// </summary>
        [Fact]
        public async Task GetClosestAsync_WithMultipleSnapshots_ReturnsClosestToTargetDate()
        {
            MetricKey testKey = new MetricKey("closest_metric");
            DateTime time1 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime time2 = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime time3 = new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc);

            await _repository.InsertAsync(testKey, time1, 10m, _session);
            await _repository.InsertAsync(testKey, time2, 20m, _session);
            await _repository.InsertAsync(testKey, time3, 30m, _session);

            DateTime targetDate = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc);
            MetricSnapshot? result = await _repository.GetClosestAsync(testKey, targetDate, _session);

            Assert.NotNull(result);
            Assert.Equal(20m, result.Value);
        }

        /// <summary>
        /// Verifies that GetClosestAsync returns null when no data exists for the key.
        /// </summary>
        [Fact]
        public async Task GetClosestAsync_WithNoData_ReturnsNull()
        {
            MetricKey testKey = new MetricKey("closest_empty_metric");
            DateTime targetDate = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

            MetricSnapshot? result = await _repository.GetClosestAsync(testKey, targetDate, _session);

            Assert.Null(result);
        }

        #endregion

        #region GetSampledRangeAsync

        /// <summary>
        /// Verifies that GetSampledRangeAsync returns data points within the specified range.
        /// </summary>
        [Fact]
        public async Task GetSampledRangeAsync_WithExistingData_ReturnsDataPoints()
        {
            MetricKey testKey = new MetricKey("sampled_metric");
            DateTime baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < 10; i++)
            {
                await _repository.InsertAsync(testKey, baseTime.AddDays(i), i * 10m, _session);
            }

            DateTime start = baseTime;
            DateTime end = baseTime.AddDays(9);
            IReadOnlyList<MetricDataPoint> result = await _repository.GetSampledRangeAsync(testKey, start, end, 5, _session);

            Assert.NotEmpty(result);
            Assert.True(result.Count <= 5);

            for (int i = 1; i < result.Count; i++)
            {
                Assert.True(result[i].CollectedAt >= result[i - 1].CollectedAt);
            }
        }

        /// <summary>
        /// Verifies that GetSampledRangeAsync returns an empty list when no data exists.
        /// </summary>
        [Fact]
        public async Task GetSampledRangeAsync_WithNoData_ReturnsEmptyList()
        {
            MetricKey testKey = new MetricKey("sampled_empty_metric");
            DateTime start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime end = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);

            IReadOnlyList<MetricDataPoint> result = await _repository.GetSampledRangeAsync(testKey, start, end, 10, _session);

            Assert.Empty(result);
        }

        /// <summary>
        /// Verifies that GetSampledRangeAsync with a single sample returns at most one data point.
        /// </summary>
        [Fact]
        public async Task GetSampledRangeAsync_WithSingleSample_ReturnsOneSample()
        {
            MetricKey testKey = new MetricKey("sampled_single_metric");
            DateTime baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < 5; i++)
            {
                await _repository.InsertAsync(testKey, baseTime.AddDays(i), i * 10m, _session);
            }

            DateTime start = baseTime;
            DateTime end = baseTime.AddDays(4);
            IReadOnlyList<MetricDataPoint> result = await _repository.GetSampledRangeAsync(testKey, start, end, 1, _session);

            Assert.True(result.Count <= 1);
        }

        #endregion

        #region GetLastCollectionTimeAsync

        /// <summary>
        /// Verifies that GetLastCollectionTimeAsync returns the latest collection timestamp.
        /// </summary>
        [Fact]
        public async Task GetLastCollectionTimeAsync_WithExistingData_ReturnsLatestTime()
        {
            MetricKey testKey = new MetricKey("last_time_metric");
            DateTime olderTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime newerTime = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc);

            await _repository.InsertAsync(testKey, olderTime, 10m, _session);
            await _repository.InsertAsync(testKey, newerTime, 20m, _session);

            DateTime? result = await _repository.GetLastCollectionTimeAsync(testKey, _session);

            Assert.NotNull(result);
            Assert.Equal(newerTime, result.Value.ToUniversalTime());
        }

        /// <summary>
        /// Verifies that GetLastCollectionTimeAsync returns null when no data exists.
        /// </summary>
        [Fact]
        public async Task GetLastCollectionTimeAsync_WithNoData_ReturnsNull()
        {
            MetricKey testKey = new MetricKey("last_time_empty_metric");

            DateTime? result = await _repository.GetLastCollectionTimeAsync(testKey, _session);

            Assert.Null(result);
        }

        #endregion

        #region EnsureSchemaAsync

        /// <summary>
        /// Verifies that EnsureSchemaAsync does not throw when the table already exists.
        /// </summary>
        [Fact]
        public async Task EnsureSchemaAsync_WhenTableExists_DoesNotThrow()
        {
            Exception? exception = await Record.ExceptionAsync(async () =>
                await _repository.EnsureSchemaAsync(_session));

            Assert.Null(exception);
        }

        #endregion
    }
}
