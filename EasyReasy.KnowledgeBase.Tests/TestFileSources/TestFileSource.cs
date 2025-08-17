using EasyReasy.KnowledgeBase.Indexing;

namespace EasyReasy.KnowledgeBase.Tests.TestFileSources
{
    /// <summary>
    /// Simple implementation of IFileSource for testing purposes.
    /// </summary>
    public class TestFileSource : IFileSource
    {
        private readonly Guid _fileId;
        private readonly Resource _resource;
        private readonly ResourceManager _resourceManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestFileSource"/> class.
        /// </summary>
        /// <param name="fileId">The unique identifier for the file.</param>
        /// <param name="resource">The resource to use as the file source.</param>
        /// <param name="resourceManager">The resource manager to access the resource.</param>
        public TestFileSource(Guid fileId, Resource resource, ResourceManager resourceManager)
        {
            _fileId = fileId;
            _resource = resource;
            _resourceManager = resourceManager;
        }

        /// <summary>
        /// Gets the unique identifier for this file.
        /// </summary>
        public Guid FileId => _fileId;

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        public string FileName => _resource.Path;

        /// <summary>
        /// Creates a new read stream for the file content.
        /// </summary>
        /// <returns>A stream that can be used to read the file content.</returns>
        public Stream CreateReadStream()
        {
            return _resourceManager.GetResourceStreamAsync(_resource).Result;
        }
    }
}
