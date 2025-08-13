using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Provides methods for reading knowledge file sections by grouping chunks based on content similarity.
    /// </summary>
    public interface IKnowledgeSectionReader
    {
        /// <summary>
        /// Reads sections from the knowledge file asynchronously, grouping chunks based on embedding similarity.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An async enumerable of chunk lists, where each list represents a section.</returns>
        IAsyncEnumerable<List<KnowledgeFileChunk>> ReadSectionsAsync(
            CancellationToken cancellationToken = default);
    }
}