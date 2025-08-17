using EasyReasy.KnowledgeBase.Indexing;

namespace EasyReasy.KnowledgeBase.Tests.TestFileSources
{
    /// <summary>
    /// In-memory implementation of IFileSourceProvider for testing purposes.
    /// </summary>
    public class InMemoryFileSourceProvider : IFileSourceProvider
    {
        private readonly List<IFileSource> _fileSources;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryFileSourceProvider"/> class.
        /// </summary>
        public InMemoryFileSourceProvider()
        {
            _fileSources = new List<IFileSource>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryFileSourceProvider"/> class with initial file sources.
        /// </summary>
        /// <param name="fileSources">The initial file sources to include.</param>
        public InMemoryFileSourceProvider(IEnumerable<IFileSource> fileSources)
        {
            _fileSources = new List<IFileSource>(fileSources);
        }

        /// <summary>
        /// Gets all available file sources.
        /// </summary>
        /// <returns>An enumerable of file sources that can be indexed.</returns>
        public async Task<IEnumerable<IFileSource>> GetAllFilesAsync()
        {
            await Task.CompletedTask;
            return _fileSources;
        }

        /// <summary>
        /// Adds a file source to the provider.
        /// </summary>
        /// <param name="fileSource">The file source to add.</param>
        public void AddFile(IFileSource fileSource)
        {
            _fileSources.Add(fileSource);
        }

        /// <summary>
        /// Adds multiple file sources to the provider.
        /// </summary>
        /// <param name="fileSources">The file sources to add.</param>
        public void AddFiles(IEnumerable<IFileSource> fileSources)
        {
            _fileSources.AddRange(fileSources);
        }

        /// <summary>
        /// Removes a file source from the provider.
        /// </summary>
        /// <param name="fileSource">The file source to remove.</param>
        /// <returns>True if the file source was found and removed; otherwise, false.</returns>
        public bool RemoveFile(IFileSource fileSource)
        {
            return _fileSources.Remove(fileSource);
        }

        /// <summary>
        /// Removes all file sources from the provider.
        /// </summary>
        public void Clear()
        {
            _fileSources.Clear();
        }

        /// <summary>
        /// Gets the number of file sources in the provider.
        /// </summary>
        public int Count => _fileSources.Count;
    }
}
