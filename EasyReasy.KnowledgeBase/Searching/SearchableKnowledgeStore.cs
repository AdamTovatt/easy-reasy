using EasyReasy.KnowledgeBase.Searching;
using EasyReasy.KnowledgeBase.Storage;

namespace EasyReasy.KnowledgeBase.Searching
{
    /// <summary>
    /// A concrete implementation of a searchable knowledge store.
    /// </summary>
    public class SearchableKnowledgeStore : ISearchableKnowledgeStore
    {
        private readonly IFileStore _fileStore;
        private readonly ISectionStore _sectionStore;
        private readonly IChunkStore _chunkStore;
        private readonly IKnowledgeVectorStore _chunksVectorStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchableKnowledgeStore"/> class.
        /// </summary>
        /// <param name="fileStore">The file store.</param>
        /// <param name="sectionStore">The section store.</param>
        /// <param name="chunkStore">The chunk store.</param>
        /// <param name="chunksVectorStore">The vector store for chunk searches.</param>
        /// <param name="sectionsVectorStore">The vector store for section searches.</param>
        public SearchableKnowledgeStore(
            IFileStore fileStore,
            ISectionStore sectionStore,
            IChunkStore chunkStore,
            IKnowledgeVectorStore chunksVectorStore)
        {
            _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
            _sectionStore = sectionStore ?? throw new ArgumentNullException(nameof(sectionStore));
            _chunkStore = chunkStore ?? throw new ArgumentNullException(nameof(chunkStore));
            _chunksVectorStore = chunksVectorStore ?? throw new ArgumentNullException(nameof(chunksVectorStore));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchableKnowledgeStore"/> class using a knowledge store.
        /// </summary>
        /// <param name="knowledgeStore">The knowledge store containing file, section, and chunk stores.</param>
        /// <param name="chunksVectorStore">The vector store for chunk searches.</param>
        /// <param name="sectionsVectorStore">The vector store for section searches.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public SearchableKnowledgeStore(
            IKnowledgeStore knowledgeStore,
            IKnowledgeVectorStore chunksVectorStore)
        {
            if (knowledgeStore == null)
                throw new ArgumentNullException(nameof(knowledgeStore));

            _fileStore = knowledgeStore.Files ?? throw new ArgumentNullException(nameof(knowledgeStore.Files));
            _sectionStore = knowledgeStore.Sections ?? throw new ArgumentNullException(nameof(knowledgeStore.Sections));
            _chunkStore = knowledgeStore.Chunks ?? throw new ArgumentNullException(nameof(knowledgeStore.Chunks));
            _chunksVectorStore = chunksVectorStore ?? throw new ArgumentNullException(nameof(chunksVectorStore));
        }

        /// <summary>
        /// Gets the file store.
        /// </summary>
        public IFileStore Files => _fileStore;

        /// <summary>
        /// Gets the section store.
        /// </summary>
        public ISectionStore Sections => _sectionStore;

        /// <summary>
        /// Gets the chunk store.
        /// </summary>
        public IChunkStore Chunks => _chunkStore;

        /// <summary>
        /// Gets the vector store for searching all chunks across all sections.
        /// </summary>
        /// <returns>A vector store for chunk-level searches.</returns>
        public IKnowledgeVectorStore GetChunksVectorStore()
        {
            return _chunksVectorStore;
        }
    }
}
