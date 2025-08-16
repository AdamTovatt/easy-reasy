using EasyReasy.KnowledgeBase.Storage;

namespace EasyReasy.KnowledgeBase.Searchings
{
    /// <summary>
    /// Defines the contract for a searchable knowledge store that provides vector-based search capabilities.
    /// </summary>
    public interface ISearchableKnowledgeStore : IKnowledgeStore
    {
        /// <summary>
        /// Gets the vector store for searching sections.
        /// </summary>
        /// <returns>A vector store for section-level searches.</returns>
        IKnowledgeVectorStore GetSectionsVectorStore();

        /// <summary>
        /// Gets the vector store for searching chunks within a specific section.
        /// </summary>
        /// <param name="sectionId">The unique identifier of the section.</param>
        /// <returns>A vector store for chunk-level searches within the specified section.</returns>
        IKnowledgeVectorStore GetChunksVectorStore(Guid sectionId);
    }
}
