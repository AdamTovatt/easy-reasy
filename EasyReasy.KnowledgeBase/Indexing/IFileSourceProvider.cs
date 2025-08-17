namespace EasyReasy.KnowledgeBase.Indexing
{
    /// <summary>
    /// Provides access to multiple file sources that can be indexed.
    /// </summary>
    public interface IFileSourceProvider
    {
        /// <summary>
        /// Gets all available file sources.
        /// </summary>
        /// <returns>An enumerable of file sources that can be indexed.</returns>
        Task<IEnumerable<IFileSource>> GetAllFilesAsync();
    }
}
