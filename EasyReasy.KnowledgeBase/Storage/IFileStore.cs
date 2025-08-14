using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage
{
    /// <summary>
    /// Defines the contract for storing and retrieving knowledge files.
    /// </summary>
    public interface IFileStore
    {
        /// <summary>
        /// Adds a knowledge file to the store.
        /// </summary>
        /// <param name="file">The knowledge file to add.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains the unique identifier of the added file.</returns>
        Task<Guid> AddAsync(KnowledgeFile file);

        /// <summary>
        /// Retrieves a knowledge file by its unique identifier.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains the file if found; otherwise, null.</returns>
        Task<KnowledgeFile?> GetAsync(Guid fileId);

        /// <summary>
        /// Checks if a knowledge file exists in the store.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file to check.</param>
        /// <returns>A task that represents the asynchronous operation. The result indicates whether the file exists.</returns>
        Task<bool> ExistsAsync(Guid fileId);

        /// <summary>
        /// Retrieves all knowledge files from the store.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The result contains a collection of all knowledge files.</returns>
        Task<IEnumerable<KnowledgeFile>> GetAllAsync();

        /// <summary>
        /// Updates an existing knowledge file in the store.
        /// </summary>
        /// <param name="file">The knowledge file to update.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task UpdateAsync(KnowledgeFile file);

        /// <summary>
        /// Deletes a knowledge file from the store.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file to delete.</param>
        /// <returns>A task that represents the asynchronous operation. The result indicates whether the file was deleted.</returns>
        Task<bool> DeleteAsync(Guid fileId);
    }
}
