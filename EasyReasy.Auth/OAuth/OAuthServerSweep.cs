using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Deletes what the authorization server can never accept again, every <see cref="OAuthServerOptions.SweepInterval"/>,
    /// through <see cref="IOAuthServerStore.DeleteExpiredAsync"/>: the one place that decides when a request, code,
    /// refresh token or client is gone.
    /// </summary>
    internal sealed class OAuthServerSweep : StoreSweep
    {
        private readonly OAuthServerSettings _settings;

        /// <summary>
        /// Initializes the sweep.
        /// </summary>
        /// <param name="scopeFactory">Creates the scope each sweep resolves the store in.</param>
        /// <param name="settings">The server's settings, for the interval and the unused client lifetime.</param>
        /// <param name="timeProvider">The clock the rows are compared with and the interval is timed by.</param>
        /// <param name="logger">Where a failed sweep is logged.</param>
        public OAuthServerSweep(IServiceScopeFactory scopeFactory, OAuthServerSettings settings, TimeProvider timeProvider, ILogger<OAuthServerSweep> logger)
            : base(scopeFactory, settings.SweepInterval, timeProvider, logger)
        {
            _settings = settings;
        }

        /// <summary>
        /// Deletes the rows expired by now, then the clients registered before now minus
        /// <see cref="OAuthServerSettings.UnusedClientLifetime"/> that nothing left names.
        /// </summary>
        protected override Task SweepAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
        {
            IOAuthServerStore store = services.GetRequiredService<IOAuthServerStore>();
            return store.DeleteExpiredAsync(now, now - _settings.UnusedClientLifetime, cancellationToken);
        }
    }
}
