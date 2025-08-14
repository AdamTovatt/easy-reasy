namespace EasyReasy.KnowledgeBase.Storage
{
    /// <summary>
    /// Defines the contract for storing and searching vector embeddings.
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Adds a vector to the store with the specified identifier.
        /// </summary>
        /// <param name="guid">The unique identifier for the vector.</param>
        /// <param name="vector">The vector to store.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task AddAsync(Guid guid, float[] vector);

        /// <summary>
        /// Removes a vector from the store by its identifier.
        /// </summary>
        /// <param name="guid">The unique identifier of the vector to remove.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task RemoveAsync(Guid guid);

        /// <summary>
        /// Searches for vectors similar to the query vector.
        /// </summary>
        /// <param name="queryVector">The vector to search for similar vectors.</param>
        /// <param name="maxResultsCount">The maximum number of results to return.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains a collection of vector identifiers ordered by similarity.</returns>
        Task<IEnumerable<Guid>> SearchAsync(float[] queryVector, int maxResultsCount);
    }
}
