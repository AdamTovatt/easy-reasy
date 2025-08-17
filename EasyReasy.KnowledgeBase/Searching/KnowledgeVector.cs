namespace EasyReasy.KnowledgeBase.Searching
{
    /// <summary>
    /// Represents a knowledge vector with an associated identifier and vector data.
    /// </summary>
    public class KnowledgeVector : IKnowledgeVector
    {
        /// <summary>
        /// Gets the unique identifier for this knowledge vector.
        /// </summary>
        public Guid Id { get; init; }

        private float[] _vector;

        /// <summary>
        /// Gets the vector representation of this knowledge vector.
        /// </summary>
        /// <returns>The vector representation of this knowledge vector.</returns>
        public float[] Vector()
        {
            return _vector ?? Array.Empty<float>();
        }

        /// <summary>
        /// Gets a value indicating whether this knowledge vector contains a valid vector.
        /// </summary>
        /// <returns>True if this knowledge vector contains a valid vector; otherwise, false.</returns>
        public bool ContainsVector()
        {
            return _vector != null && _vector.Length > 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeVector"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the knowledge vector.</param>
        /// <param name="vector">The vector data.</param>
        public KnowledgeVector(Guid id, float[] vector)
        {
            Id = id;
            _vector = vector;
        }
    }
}
