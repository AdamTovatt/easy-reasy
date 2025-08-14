using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage
{
    /// <summary>
    /// Defines the contract for storing and retrieving knowledge file sections.
    /// </summary>
    public interface ISectionStore
    {
        /// <summary>
        /// Adds a section to the store.
        /// </summary>
        /// <param name="section">The section to add.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task AddAsync(KnowledgeFileSection section);

        /// <summary>
        /// Retrieves a section by its unique identifier.
        /// </summary>
        /// <param name="sectionId">The unique identifier of the section.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains the section if found; otherwise, null.</returns>
        Task<KnowledgeFileSection?> GetAsync(Guid sectionId);

        /// <summary>
        /// Retrieves a section by its index within a specific file.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file.</param>
        /// <param name="sectionIndex">The zero-based index of the section within the file.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains the section if found; otherwise, null.</returns>
        Task<KnowledgeFileSection?> GetByIndexAsync(Guid fileId, int sectionIndex);

        /// <summary>
        /// Retrieves the next section in sequence within a specific file.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file.</param>
        /// <param name="currentSectionIndex">The zero-based index of the current section.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains the next section if found; otherwise, null.</returns>
        Task<KnowledgeFileSection?> GetNextAsync(Guid fileId, int currentSectionIndex);

        /// <summary>
        /// Retrieves the previous section in sequence within a specific file.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file.</param>
        /// <param name="currentSectionIndex">The zero-based index of the current section.</param>
        /// <returns>A task that represents the asynchronous operation. The result contains the previous section if found; otherwise, null.</returns>
        Task<KnowledgeFileSection?> GetPreviousAsync(Guid fileId, int currentSectionIndex);

        /// <summary>
        /// Deletes all sections belonging to a specific file.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file whose sections should be deleted.</param>
        /// <returns>A task that represents the asynchronous operation. The result indicates whether any sections were deleted.</returns>
        Task<bool> DeleteByFileAsync(Guid fileId);
    }
}
