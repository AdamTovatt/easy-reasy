using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Deletes what the authorization server can never accept again, every <see cref="OAuthServerOptions.SweepInterval"/>,
    /// through <see cref="IOAuthServerStore.DeleteExpiredAsync"/>: the one place that decides when a request, code,
    /// refresh token or client is gone. The store is scoped, so each sweep resolves it in a scope of its own.
    /// </summary>
    internal sealed class OAuthServerSweep : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly OAuthServerSettings _settings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<OAuthServerSweep> _logger;

        /// <summary>
        /// Initializes the sweep.
        /// </summary>
        /// <param name="scopeFactory">Creates the scope each sweep resolves the store in.</param>
        /// <param name="settings">The server's settings, for the interval and the unused client lifetime.</param>
        /// <param name="timeProvider">The clock the rows are compared with and the interval is timed by.</param>
        /// <param name="logger">Where a failed sweep is logged.</param>
        public OAuthServerSweep(IServiceScopeFactory scopeFactory, OAuthServerSettings settings, TimeProvider timeProvider, ILogger<OAuthServerSweep> logger)
        {
            _scopeFactory = scopeFactory;
            _settings = settings;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// Sweeps once: deletes the rows expired by now, then the clients registered before now minus
        /// <see cref="OAuthServerSettings.UnusedClientLifetime"/> that nothing left names.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the sweep.</param>
        public async Task SweepOnceAsync(CancellationToken cancellationToken)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IOAuthServerStore store = scope.ServiceProvider.GetRequiredService<IOAuthServerStore>();
            await store.DeleteExpiredAsync(now, now - _settings.UnusedClientLifetime, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(_settings.SweepInterval, _timeProvider);
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
                        // A failed sweep, a store's timeout among them, leaves the rows for the next one; nothing it would
                        // have deleted is ever accepted meanwhile.
                        _logger.LogError(exception, "The OAuth server's sweep of expired rows failed; the next runs in {Interval}.", _settings.SweepInterval);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }
}
