using EasyReasy.Metrics.Models;

namespace EasyReasy.Metrics.Repository
{
    /// <summary>
    /// Internal database entity representing a row in the <c>metric_snapshot</c> table.
    /// Used for Dapper/Mapping deserialization with snake_case to PascalCase automatic mapping.
    /// </summary>
    internal class MetricSnapshotEntity
    {
        /// <summary>
        /// Gets or sets the unique database identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the string metric key from the database.
        /// </summary>
        public required string MetricKey { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the metric was collected.
        /// </summary>
        public DateTime CollectedAt { get; set; }

        /// <summary>
        /// Gets or sets the numeric value of the metric.
        /// </summary>
        public decimal Value { get; set; }

        /// <summary>
        /// Converts this database entity to the domain <see cref="MetricSnapshot"/> model.
        /// </summary>
        /// <returns>A <see cref="MetricSnapshot"/> domain model.</returns>
        public MetricSnapshot ToDomain()
        {
            return new MetricSnapshot
            {
                Id = Id,
                MetricKey = new MetricKey(MetricKey),
                CollectedAt = CollectedAt,
                Value = Value
            };
        }
    }
}
