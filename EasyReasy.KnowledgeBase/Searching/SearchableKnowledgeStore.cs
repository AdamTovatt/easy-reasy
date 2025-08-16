using EasyReasy.KnowledgeBase.Searchings;
using EasyReasy.KnowledgeBase.Storage;

namespace EasyReasy.KnowledgeBase.Searching
{
    /// <summary>
    /// A concrete implementation of a searchable knowledge store.
    /// </summary>
    public class SearchableKnowledgeStore : ISearchableKnowledgeStore
    {
        /// <summary>
        /// Gets the file store. This implementation throws <see cref="NotImplementedException"/>.
        /// </summary>
        public IFileStore Files => throw new NotImplementedException();

        /// <summary>
        /// Gets the section store. This implementation throws <see cref="NotImplementedException"/>.
        /// </summary>
        public ISectionStore Sections => throw new NotImplementedException();

        /// <summary>
        /// Gets the chunk store. This implementation throws <see cref="NotImplementedException"/>.
        /// </summary>
        public IChunkStore Chunks => throw new NotImplementedException();

        /// <summary>
        /// Gets the vector store for searching chunks within a specific section.
        /// This implementation throws <see cref="NotImplementedException"/>.
        /// </summary>
        /// <param name="sectionId">The unique identifier of the section.</param>
        /// <returns>A vector store for chunk-level searches.</returns>
        public IKnowledgeVectorStore GetChunksVectorStore(Guid sectionId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the vector store for searching files.
        /// This implementation throws <see cref="NotImplementedException"/>.
        /// </summary>
        /// <returns>A vector store for file-level searches.</returns>
        public IKnowledgeVectorStore GetFilesVectorStore()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the vector store for searching sections.
        /// This implementation throws <see cref="NotImplementedException"/>.
        /// </summary>
        /// <returns>A vector store for section-level searches.</returns>
        public IKnowledgeVectorStore GetSectionsVectorStore()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the vector store for searching sections within a specific file.
        /// This implementation throws <see cref="NotImplementedException"/>.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file.</param>
        /// <returns>A vector store for section-level searches within the specified file.</returns>
        public IKnowledgeVectorStore GetSectionsVectorStore(Guid fileId)
        {
            throw new NotImplementedException();
        }
    }
}
