using EasyReasy.KnowledgeBase.Searching;

namespace EasyReasy.KnowledgeBase
{
    /// <summary>
    /// Represents a knowledge base that is searchable.
    /// </summary>
    public interface ISearchableKnowledgeBase
    {
        /// <summary>
        /// Searches in the knowledgebase for relevant content.
        /// </summary>
        /// <param name="query">The query to search with.</param>
        /// <param name="maxSearchResultsCount">The maximum number of search results to return.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the search operation.</param>
        /// <returns>An <see cref="IKnowledgeBaseSearchResult"/> that can provide search result status and content.</returns>
        Task<IKnowledgeBaseSearchResult> SearchAsync(string query, int? maxSearchResultsCount = null, CancellationToken cancellationToken = default);
    }
}
