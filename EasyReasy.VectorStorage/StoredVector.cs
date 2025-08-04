namespace EasyReasy.VectorStorage
{
    public readonly struct StoredVector
    {
        public readonly Guid Id;
        public readonly float[] Values;

        /// <summary>
        /// Gets a read-only span over the vector values for high-performance operations
        /// </summary>
        /// <returns>A read-only span over the vector values</returns>
        public ReadOnlySpan<float> GetSpan() => Values.AsSpan();

        public StoredVector(Guid id, float[] values)
        {
            Id = id;
            Values = values;
        }
    }
}
