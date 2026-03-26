namespace EasyReasy.Metrics.Collection
{
    /// <summary>
    /// Interface for a metric collector that produces a single numeric value for a specific metric key.
    /// Collectors participate in dependency injection and can depend on repositories, HTTP clients,
    /// or any other service.
    /// </summary>
    public interface IMetricCollector
    {
        /// <summary>
        /// Gets the metric key that this collector produces values for.
        /// </summary>
        MetricKey MetricKey { get; }

        /// <summary>
        /// Collects the current value of the metric.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        /// <returns>The current numeric value of the metric.</returns>
        Task<decimal> CollectAsync(CancellationToken cancellationToken);
    }
}
