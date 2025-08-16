namespace EasyReasy.KnowledgeBase.Searching
{
    /// <summary>
    /// Represents the search result of a search performed in a knowledge base.
    /// </summary>
    public interface IKnowledgeBaseSearchResult
    {
        /// <summary>
        /// Gets the search result as a context string.
        /// </summary>
        /// <returns>A string that can be directly passed to an LLM to give it context.</returns>
        string GetAsContextString();

        /// <summary>
        /// Gets whether or not the search that was performed to get this result was a success.
        /// </summary>
        bool WasSuccess { get; }

        /// <summary>
        /// Gets whether or not the search that was performed to get this result can be retried.
        /// </summary>
        bool CanBeRetried { get; }

        /// <summary>
        /// Gets whether or not the search that was performed to get this result should be retired.
        /// </summary>
        bool ShouldBeRetried { get; }

        /// <summary>
        /// Gets the error message that the search to get this search result resulted in if any. Null if no error was to be reported.
        /// </summary>
        string? ErrorMessage { get; }
    }
}
