using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// <see cref="StoreSweep"/>'s own rules, through a sweep of a test's own: the interval it accepts, the scope and clock
    /// each sweep gets, and a failure that does not stop the next. <see cref="OAuthServerSweepTests"/> covers it through
    /// the OAuth server's sweep.
    /// </summary>
    [TestClass]
    public class StoreSweepTests
    {
        [TestMethod]
        [DataRow(0L, DisplayName = "zero")]
        [DataRow(-1L, DisplayName = "negative")]
        public void Constructor_IntervalNotPositive_Throws(long ticks)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RecordingSweep(TimeSpan.FromTicks(ticks)));
        }

        [TestMethod]
        public void Constructor_IntervalOverTheTimersLongestPeriod_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RecordingSweep(StoreSweep.MaxInterval + TimeSpan.FromTicks(1)));
        }

        [TestMethod]
        public void Constructor_IntervalAtItsLongest_IsAccepted()
        {
            RecordingSweep sweep = new RecordingSweep(StoreSweep.MaxInterval);

            Assert.AreEqual(StoreSweep.MaxInterval, sweep.Interval);
        }

        [TestMethod]
        public async Task SweepOnceAsync_Called_SweepsInAScopeOfItsOwnAtTheClocksTime()
        {
            FakeTimeProvider clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
            RecordingSweep sweep = new RecordingSweep(TimeSpan.FromHours(1), clock);

            await sweep.SweepOnceAsync(CancellationToken.None);
            await sweep.SweepOnceAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new[] { clock.GetUtcNow().UtcDateTime, clock.GetUtcNow().UtcDateTime }, sweep.Times);
            Assert.AreEqual(2, sweep.Scopes.Distinct().Count());
        }

        [TestMethod]
        public async Task ExecuteAsync_SweepFails_LogsUnderTheFeaturesCategoryAndSweepsAgain()
        {
            RecordingLoggerProvider logs = new RecordingLoggerProvider();
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
            RecordingSweep sweep = new RecordingSweep(TimeSpan.FromMilliseconds(20), logger: loggerFactory.CreateLogger("Feature.Sweep")) { FailuresLeft = 1 };

            await sweep.StartAsync(CancellationToken.None);
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (sweep.Times.Count == 0)
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, "Timed out waiting for the sweep after the failed one.");
                await Task.Delay(10);
            }

            await sweep.StopAsync(CancellationToken.None);

            RecordingLoggerProvider.Entry error = logs.Entries.Single(entry => entry.Level == LogLevel.Error);
            Assert.AreEqual("Feature.Sweep", error.Category);
            Assert.IsTrue(sweep.ExecuteTask!.IsCompletedSuccessfully, $"The sweep ended {sweep.ExecuteTask.Status}.");
        }

        /// <summary>
        /// A sweep that records each run's time and scope, and can fail its first runs.
        /// </summary>
        private sealed class RecordingSweep : StoreSweep
        {
            public RecordingSweep(TimeSpan interval, TimeProvider? timeProvider = null, ILogger? logger = null)
                : base(CreateScopeFactory(), interval, timeProvider ?? TimeProvider.System, logger ?? NullLogger.Instance)
            {
            }

            public int FailuresLeft { get; set; }

            public List<DateTime> Times { get; } = new List<DateTime>();

            public List<IServiceProvider> Scopes { get; } = new List<IServiceProvider>();

            protected override Task SweepAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
            {
                if (FailuresLeft > 0)
                {
                    FailuresLeft--;
                    throw new InvalidOperationException("The store is down.");
                }

                Scopes.Add(services);
                Times.Add(now);
                return Task.CompletedTask;
            }

            private static IServiceScopeFactory CreateScopeFactory()
            {
                return new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            }
        }
    }
}
