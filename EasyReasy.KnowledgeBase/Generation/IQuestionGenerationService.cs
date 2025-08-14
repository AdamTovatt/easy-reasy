namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Service for generating synthetic questions from text content for RAG/retrieval research.
    /// </summary>
    public interface IQuestionGenerationService
    {
        /// <summary>
        /// Generates synthetic questions from the provided text content.
        /// </summary>
        /// <param name="text">The text content to generate questions from.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous question generation operation. The task result contains the list of generated questions.</returns>
        Task<List<string>> GenerateQuestionsAsync(string text, CancellationToken cancellationToken = default);
    }
} 