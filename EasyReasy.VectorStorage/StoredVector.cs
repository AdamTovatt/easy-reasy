namespace EasyReasy.VectorStorage
{
    /// <summary>
    /// Represents a vector stored in the vector store with its associated metadata.
    /// </summary>
    public readonly struct StoredVector
    {
        /// <summary>
        /// Gets the unique identifier of the vector.
        /// </summary>
        public readonly Guid Id;

        /// <summary>
        /// Gets the vector values as a float array.
        /// </summary>
        public readonly float[] Values;

        /// <summary>
        /// Gets a read-only span over the vector values for high-performance operations
        /// </summary>
        /// <returns>A read-only span over the vector values</returns>
        public ReadOnlySpan<float> GetSpan() => Values.AsSpan();

        /// <summary>
        /// Initializes a new instance of the <see cref="StoredVector"/> struct with the specified ID and values.
        /// </summary>
        /// <param name="id">The unique identifier of the vector.</param>
        /// <param name="values">The vector values.</param>
        public StoredVector(Guid id, float[] values)
        {
            Id = id;
            Values = values;
        }
    }
}
