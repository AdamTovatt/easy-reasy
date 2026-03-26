namespace EasyReasy.Metrics.Configuration
{
    /// <summary>
    /// Configuration options for the EasyReasy.Metrics library, controlling collection timing
    /// and deduplication behavior.
    /// </summary>
    public class EasyReasyMetricsOptions
    {
        /// <summary>
        /// Gets or sets the interval between metric collection runs.
        /// Defaults to 5 hours.
        /// </summary>
        public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromHours(5);

        /// <summary>
        /// Gets or sets the minimum time that must elapse between collections of the same metric key.
        /// If a metric was collected more recently than this interval, the collection is skipped.
        /// Defaults to 30 minutes.
        /// </summary>
        public TimeSpan MinimumTimeBetweenCollections { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets the initial delay before the first metric collection run after application startup.
        /// Defaults to 30 seconds.
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Validates the options and throws if any values are invalid.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <see cref="CollectionInterval"/>, <see cref="MinimumTimeBetweenCollections"/>,
        /// or <see cref="InitialDelay"/> is zero or negative.
        /// </exception>
        internal void Validate()
        {
            if (CollectionInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CollectionInterval), "Must be a positive time span.");
            }

            if (MinimumTimeBetweenCollections <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MinimumTimeBetweenCollections), "Must be a positive time span.");
            }

            if (InitialDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(InitialDelay), "Must be non-negative.");
            }
        }
    }
}
