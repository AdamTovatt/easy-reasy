namespace EasyReasy.KnowledgeBase.Indexing
{
    /// <summary>
    /// Represents a service that can index documents from file sources.
    /// </summary>
    public interface IIndexer
    {
        /// <summary>
        /// Consumes a file source and indexes its content.
        /// </summary>
        /// <param name="fileSource">The file source to index.</param>
        /// <returns>A task that represents the asynchronous indexing operation. Returns true if content was indexed, false if the file was already up to date.</returns>
        Task<bool> ConsumeAsync(IFileSource fileSource);
    }
}
