using EasyReasy.KnowledgeBase.ConfidenceRating;
using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Searching
{
    /// <summary>
    /// Concrete implementation of a knowledge base search result.
    /// </summary>
    public class KnowledgeBaseSearchResult : IKnowledgeBaseSearchResult
    {
        /// <summary>
        /// Gets whether the search was successful.
        /// </summary>
        public bool WasSuccess { get; }

        /// <summary>
        /// Gets whether the search can be retried.
        /// </summary>
        public bool CanBeRetried { get; }

        /// <summary>
        /// Gets whether the search should be retried.
        /// </summary>
        public bool ShouldBeRetried { get; }

        /// <summary>
        /// Gets the error message if the search failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the relevant sections with their relevance metrics.
        /// </summary>
        public IReadOnlyList<RelevanceRatedEntry<KnowledgeFileSection>> RelevantSections { get; }

        /// <summary>
        /// Gets the original search query.
        /// </summary>
        public string Query { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KnowledgeBaseSearchResult"/> class.
        /// </summary>
        /// <param name="relevantSections">The relevant sections with their relevance metrics.</param>
        /// <param name="query">The original search query.</param>
        /// <param name="wasSuccess">Whether the search was successful.</param>
        /// <param name="canBeRetried">Whether the search can be retried.</param>
        /// <param name="shouldBeRetried">Whether the search should be retried.</param>
        /// <param name="errorMessage">The error message if the search failed.</param>
        public KnowledgeBaseSearchResult(
            IReadOnlyList<RelevanceRatedEntry<KnowledgeFileSection>> relevantSections,
            string query,
            bool wasSuccess = true,
            bool canBeRetried = false,
            bool shouldBeRetried = false,
            string? errorMessage = null)
        {
            RelevantSections = relevantSections;
            Query = query;
            WasSuccess = wasSuccess;
            CanBeRetried = canBeRetried;
            ShouldBeRetried = shouldBeRetried;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates an error result.
        /// </summary>
        /// <param name="query">The original search query.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="canBeRetried">Whether the search can be retried.</param>
        /// <param name="shouldBeRetried">Whether the search should be retried.</param>
        /// <returns>A new error result.</returns>
        public static KnowledgeBaseSearchResult CreateError(string query, string errorMessage, bool canBeRetried = false, bool shouldBeRetried = false)
        {
            return new KnowledgeBaseSearchResult(
                relevantSections: Array.Empty<RelevanceRatedEntry<KnowledgeFileSection>>(),
                query: query,
                wasSuccess: false,
                canBeRetried: canBeRetried,
                shouldBeRetried: shouldBeRetried,
                errorMessage: errorMessage);
        }

        /// <summary>
        /// Gets the search result as a context string that can be passed to an LLM.
        /// </summary>
        /// <returns>A string containing the relevant content.</returns>
        public string GetAsContextString()
        {
            if (!WasSuccess || RelevantSections.Count == 0)
            {
                return string.Empty;
            }

            // Return sections ordered by relevance, with their content
            return string.Join("\n\n", 
                RelevantSections
                    .OrderByDescending(r => r.Relevance.CosineSimilarity)
                    .Select(r => r.Item.ToString()));
        }
    }
} 