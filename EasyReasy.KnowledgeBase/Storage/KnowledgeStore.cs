namespace EasyReasy.KnowledgeBase.Storage
{
    /// <summary>
    /// Provides a unified interface for managing knowledge files, chunks, and sections.
    /// </summary>
    public sealed class KnowledgeStore : IKnowledgeStore
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeStore"/> class with the specified storage components.
        /// </summary>
        /// <param name="files">The file store for managing knowledge files.</param>
        /// <param name="chunks">The chunk store for managing knowledge file chunks.</param>
        /// <param name="sections">The section store for managing knowledge file sections.</param>
        public KnowledgeStore(IFileStore files, IChunkStore chunks, ISectionStore sections)
        {
            this.Files = files;
            this.Chunks = chunks;
            this.Sections = sections;
        }

        /// <summary>
        /// Gets the file store for managing knowledge files.
        /// </summary>
        public IFileStore Files { get; }

        /// <summary>
        /// Gets the chunk store for managing knowledge file chunks.
        /// </summary>
        public IChunkStore Chunks { get; }

        /// <summary>
        /// Gets the section store for managing knowledge file sections.
        /// </summary>
        public ISectionStore Sections { get; }
    }
}
