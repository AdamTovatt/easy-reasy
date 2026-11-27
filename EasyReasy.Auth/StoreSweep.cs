using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A hosted service that deletes, every <see cref="Interval"/>, the rows an application-owned store keeps for a
    /// feature and can never accept again: the OAuth server's expired requests, codes and refresh tokens, or a sign-in
    /// package's expired pending requests. Each sweep runs in a scope of its own, so the store may be scoped. A failed
    /// sweep, a store's timeout among them, is logged and leaves the rows for the next one, so nothing it would have
    /// deleted is accepted meanwhile.
    /// </summary>
    /// <remarks>
    /// A feature derives from it, implements <see cref="SweepAsync"/> with its store's delete, and registers it with
    /// <c>AddHostedService</c>. Every application instance runs its own sweep, so the store's delete must be safe beside
    /// another one running at the same time.
    /// </remarks>
    public abstract class StoreSweep : BackgroundService
    {
        /// <summary>
        /// The longest interval: a <see cref="PeriodicTimer"/> refuses a period over about 49.7 days.
        /// </summary>
        public static readonly TimeSpan MaxInterval = TimeSpan.FromDays(49);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes the sweep.
        /// </summary>
        /// <param name="scopeFactory">Creates the scope each sweep runs in.</param>
        /// <param name="interval">How often the sweep runs; positive and at most <see cref="MaxInterval"/>.</param>
        /// <param name="timeProvider">The clock the rows are compared with and the interval is timed by.</param>
        /// <param name="logger">Where a failed sweep is logged, under the feature's own category.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is not positive or is over
        /// <see cref="MaxInterval"/>.</exception>
        protected StoreSweep(IServiceScopeFactory scopeFactory, TimeSpan interval, TimeProvider timeProvider, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(scopeFactory);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(interval, MaxInterval);

            _scopeFactory = scopeFactory;
            Interval = interval;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// How often the sweep runs.
        /// </summary>
        public TimeSpan Interval { get; }

        /// <summary>
        /// Sweeps once, now, in a scope of its own.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the sweep.</param>
        public async Task SweepOnceAsync(CancellationToken cancellationToken)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            await SweepAsync(scope.ServiceProvider, now, cancellationToken);
        }

        /// <summary>
        /// Deletes the rows expired by <paramref name="now"/>, through the store resolved from
        /// <paramref name="services"/>.
        /// </summary>
        /// <param name="services">The sweep's scoped services.</param>
        /// <param name="now">The UTC time the rows are compared with.</param>
        /// <param name="cancellationToken">A token to cancel the sweep.</param>
        protected abstract Task SweepAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken);

        /// <inheritdoc />
        protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(Interval, _timeProvider);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await SweepOnceAsync(stoppingToken);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogError(exception, "The sweep of expired rows failed; the next runs in {Interval}.", Interval);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }
}
