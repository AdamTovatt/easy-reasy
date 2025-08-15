using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage
{
    /// <summary>
    /// Defines the contract for storing and retrieving knowledge file chunks.
    /// </summary>
    public interface IChunkStore
    {
        /// <summary>
        /// Adds a chunk to the store.
        /// </summary>
        /// <param name="chunk">The chunk to add.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task AddAsync(KnowledgeFileChunk chunk);

        /// <summary>
        /// Retrieves a chunk by its unique identifier.
        /// </summary>
        /// <param name="chunkId">The unique identifier of the chunk.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains the chunk if found; otherwise, null.</returns>
        Task<KnowledgeFileChunk?> GetAsync(Guid chunkId);

        /// <summary>
        /// Deletes all chunks belonging to a specific file.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file whose chunks should be deleted.</param>
        /// <returns>A task that represents the asynchronous operation. The result indicates whether any chunks were deleted.</returns>
        Task<bool> DeleteByFileAsync(Guid fileId);

        /// <summary>
        /// Retrieves a chunk by its index within a specific section.
        /// </summary>
        /// <param name="sectionId">The unique identifier of the section.</param>
        /// <param name="chunkIndex">The zero-based index of the chunk within the section.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains the chunk if found; otherwise, null.</returns>
        Task<KnowledgeFileChunk?> GetByIndexAsync(Guid sectionId, int chunkIndex);
    }
}
