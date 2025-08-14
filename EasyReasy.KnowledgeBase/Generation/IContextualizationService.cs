namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Service for providing contextual information about a text snippet within surrounding content.
    /// </summary>
    public interface IContextualizationService
    {
        /// <summary>
        /// Generates contextual information for a text snippet within surrounding content.
        /// </summary>
        /// <param name="textSnippet">The shorter piece of text to be contextualized.</param>
        /// <param name="surroundingContent">The surrounding content that provides context for the text snippet.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous contextualization operation. The task result contains the contextual information.</returns>
        Task<string> ContextualizeAsync(string textSnippet, string surroundingContent, CancellationToken cancellationToken = default);
    }
} 