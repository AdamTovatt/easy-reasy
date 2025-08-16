using EasyReasy.KnowledgeBase.Searching;

namespace EasyReasy.KnowledgeBase
{
    /// <summary>
    /// Represents a knowledge base that is searchable.
    /// </summary>
    public interface ISearchableKnowledgeBase
    {
        /// <summary>
        /// Searches in the knowledgebase, trying to return a resulting context that is around the targetTokenCount in size.
        /// </summary>
        /// <param name="query">The query to search with.</param>
        /// <param name="targetTokenCount">The target amount of tokens.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the search operation.</param>
        /// <returns>An <see cref="IKnowledgeBaseSearchResult"/> that can provide search result status and content.</returns>
        Task<IKnowledgeBaseSearchResult> SearchAsync(string query, int targetTokenCount, CancellationToken cancellationToken = default);
    }
}
