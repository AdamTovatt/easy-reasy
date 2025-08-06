namespace EasyReasy.VectorStorage
{
    /// <summary>
    /// Defines the interface for a vector store that can store and search high-dimensional vectors.
    /// This interface provides methods for adding, removing, finding similar vectors, and persisting
    /// the vector store to/from streams.
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Saves the current state of the vector store to the provided stream
        /// </summary>
        /// <param name="stream">The writable stream to save to</param>
        Task SaveAsync(Stream stream);

        /// <summary>
        /// Loads vectors from the provided stream into memory
        /// </summary>
        /// <param name="stream">The readable stream to load from</param>
        Task LoadAsync(Stream stream);

        /// <summary>
        /// Adds a vector to the store
        /// </summary>
        /// <param name="vector">The vector to add</param>
        Task AddAsync(StoredVector vector);

        /// <summary>
        /// Removes a vector from the store by its ID
        /// </summary>
        /// <param name="id">The ID of the vector to remove</param>
        /// <returns>True if the vector was found and removed, false otherwise</returns>
        Task<bool> RemoveAsync(Guid id);

        /// <summary>
        /// Finds the most similar vectors to the given query vector using cosine similarity
        /// </summary>
        /// <param name="queryVector">The query vector to compare against</param>
        /// <param name="count">The maximum number of similar vectors to return</param>
        /// <returns>A collection of the most similar vectors, ordered by similarity (highest first)</returns>
        Task<IEnumerable<StoredVector>> FindMostSimilarAsync(float[] queryVector, int count);
    }
}