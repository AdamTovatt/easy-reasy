namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Service for performing one-shot text processing tasks using AI models.
    /// Examples include summarization, question extraction, entity extraction, etc.
    /// </summary>
    public interface IOneShotService
    {
        /// <summary>
        /// Performs a one-shot text processing task.
        /// </summary>
        /// <param name="systemPrompt">The system prompt that defines the task and output format.</param>
        /// <param name="userInput">The input text to process.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous processing operation. The task result contains the processed output.</returns>
        Task<string> ProcessAsync(string systemPrompt, string userInput, CancellationToken cancellationToken = default);
    }
}