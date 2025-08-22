namespace EasyReasy.KnowledgeBase.Indexing
{
    /// <summary>
    /// Represents a source of file data that can be indexed.
    /// </summary>
    public interface IFileSource
    {
        /// <summary>
        /// Gets the unique identifier for this file.
        /// </summary>
        Guid FileId { get; }

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// Creates a new read stream for the file content.
        /// </summary>
        /// <returns>A stream that can be used to read the file content.</returns>
        Task<Stream> CreateReadStreamAsync();
    }
}
