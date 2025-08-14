namespace EasyReasy.KnowledgeBase.Storage
{
    /// <summary>
    /// Defines the contract for a knowledge store that provides access to files, chunks, and sections.
    /// </summary>
    public interface IKnowledgeStore
    {
        /// <summary>
        /// Gets the file store for managing knowledge files.
        /// </summary>
        IFileStore Files { get; }

        /// <summary>
        /// Gets the chunk store for managing knowledge file chunks.
        /// </summary>
        IChunkStore Chunks { get; }

        /// <summary>
        /// Gets the section store for managing knowledge file sections.
        /// </summary>
        ISectionStore Sections { get; }
    }
}