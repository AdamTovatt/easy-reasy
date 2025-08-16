using EasyReasy.KnowledgeBase.Searching;
using EasyReasy.VectorStorage;

namespace EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore
{
    /// <summary>
    /// An implementation of <see cref="IKnowledgeVectorStore"/> that uses an underlying <see cref="IVectorStore"/> from <see cref="EasyReasy.VectorStorage"/>.
    /// </summary>
    public class EasyReasyVectorStore : IKnowledgeVectorStore
    {
        private IVectorStore _underlyingStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyReasyVectorStore"/> class.
        /// </summary>
        /// <param name="vectorStore">The underlying vector store to use for operations.</param>
        public EasyReasyVectorStore(IVectorStore vectorStore)
        {
            _underlyingStore = vectorStore;
        }

        /// <summary>
        /// Adds a vector to the store with the specified identifier.
        /// </summary>
        /// <param name="guid">The unique identifier for the vector.</param>
        /// <param name="vector">The vector to store.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task AddAsync(Guid guid, float[] vector)
        {
            return _underlyingStore.AddAsync(new StoredVector(guid, vector));
        }

        /// <summary>
        /// Removes a vector from the store by its identifier.
        /// </summary>
        /// <param name="guid">The unique identifier of the vector to remove.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task RemoveAsync(Guid guid)
        {
            return _underlyingStore.RemoveAsync(guid);
        }

        /// <summary>
        /// Searches for vectors similar to the query vector.
        /// </summary>
        /// <param name="queryVector">The vector to search for similar vectors.</param>
        /// <param name="maxResultsCount">The maximum number of results to return.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains a collection of knowledge vectors ordered by similarity.</returns>
        public async Task<IEnumerable<IKnowledgeVector>> SearchAsync(float[] queryVector, int maxResultsCount)
        {
            IEnumerable<StoredVector> underlyingSearchResult = await _underlyingStore.FindMostSimilarAsync(queryVector, maxResultsCount);

            List<KnowledgeVector> result = new List<KnowledgeVector>();

            foreach (StoredVector storedVector in underlyingSearchResult)
            {
                KnowledgeVector knowledgeVector = new KnowledgeVector(storedVector.Id, storedVector.Values);
                result.Add(knowledgeVector);
            }

            return result;
        }
    }
}
