namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Provides synthetic question generation from text content using any underlying one-shot service.
    /// </summary>
    public class QuestionGenerationService : OneShotServiceBase, IQuestionGenerationService
    {
        private const string DefaultSystemPrompt = "Generate 3-5 diverse questions that someone might ask about this content. " +
            "Include different types of questions: factual, conceptual, and application-based. " +
            "Questions should be natural and cover the key topics, concepts, and information in the text. " +
            "Format as a numbered list with one question per line.";

        private readonly string _systemPrompt;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuestionGenerationService"/> class.
        /// </summary>
        /// <param name="oneShotService">The one-shot service to use for question generation.</param>
        /// <param name="systemPrompt">An optional system prompt to guide the question generation style.</param>
        public QuestionGenerationService(IOneShotService oneShotService, string? systemPrompt = null) 
            : base(oneShotService)
        {
            _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
        }

        /// <summary>
        /// Generates synthetic questions from the provided text content using the underlying one-shot service.
        /// </summary>
        /// <param name="text">The text content to generate questions from.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>List of generated questions about the text content.</returns>
        public async Task<List<string>> GenerateQuestionsAsync(string text, CancellationToken cancellationToken = default)
        {
            string response = await ProcessAsync(_systemPrompt, text, cancellationToken);
            
            // Split by lines and extract questions using regex pattern for numbered lists
            return response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                         .Select(line => line.Trim())
                         .Where(line => !string.IsNullOrWhiteSpace(line))
                         .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"^\d+\.\s*", ""))
                         .Where(question => !string.IsNullOrWhiteSpace(question))
                         .ToList();
        }
    }
} 