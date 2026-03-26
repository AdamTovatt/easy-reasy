namespace EasyReasy.Metrics
{
    /// <summary>
    /// A strongly-typed identifier for a metric, wrapping a non-empty string key.
    /// Provides value semantics so that two <see cref="MetricKey"/> instances with the
    /// same underlying string are considered equal.
    /// </summary>
    public readonly struct MetricKey : IEquatable<MetricKey>
    {
        /// <summary>
        /// Gets the string key that identifies this metric.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricKey"/> struct.
        /// </summary>
        /// <param name="key">The string key identifying the metric. Must not be null, empty, or whitespace.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
        public MetricKey(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            Key = key;
        }

        /// <summary>
        /// Determines whether this <see cref="MetricKey"/> is equal to another <see cref="MetricKey"/>.
        /// </summary>
        /// <param name="other">The other <see cref="MetricKey"/> to compare with.</param>
        /// <returns><c>true</c> if the keys are equal; otherwise, <c>false</c>.</returns>
        public bool Equals(MetricKey other) => Key == other.Key;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is MetricKey other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => Key.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => Key;

        /// <summary>
        /// Determines whether two <see cref="MetricKey"/> instances are equal.
        /// </summary>
        public static bool operator ==(MetricKey left, MetricKey right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="MetricKey"/> instances are not equal.
        /// </summary>
        public static bool operator !=(MetricKey left, MetricKey right) => !left.Equals(right);
    }
}
