namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Service for generating summaries of text content.
    /// </summary>
    public interface ISummarizationService
    {
        /// <summary>
        /// Generates a summary for the provided text.
        /// </summary>
        /// <param name="text">The text to summarize.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous summarization operation. The task result contains the generated summary.</returns>
        Task<string> SummarizeAsync(string text, CancellationToken cancellationToken = default);
    }
} 