using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Deletes the pending requests and completions that can never be accepted again, every
    /// <see cref="SamlAuthOptions.SweepInterval"/>, through <see cref="ISamlAuthStore.DeleteExpiredAsync"/>.
    /// </summary>
    internal sealed class SamlAuthSweep : StoreSweep
    {
        /// <summary>
        /// Initializes the sweep.
        /// </summary>
        /// <param name="scopeFactory">Creates the scope each sweep resolves the store in.</param>
        /// <param name="options">The registered options, which hold the interval.</param>
        /// <param name="timeProvider">The clock the rows are compared with and the interval is timed by.</param>
        /// <param name="logger">Where a failed sweep is logged.</param>
        public SamlAuthSweep(IServiceScopeFactory scopeFactory, SamlAuthOptions options, TimeProvider timeProvider, ILogger<SamlAuthSweep> logger)
            : base(scopeFactory, options.SweepInterval, timeProvider, logger)
        {
        }

        /// <inheritdoc />
        protected override Task SweepAsync(IServiceProvider services, DateTime now, CancellationToken cancellationToken)
        {
            return services.GetRequiredService<ISamlAuthStore>().DeleteExpiredAsync(now, cancellationToken);
        }
    }
}
