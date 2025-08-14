namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Provides document summarization using any underlying one-shot service.
    /// </summary>
    public class SummarizationService : OneShotServiceBase, ISummarizationService
    {
        private const string DefaultSystemPrompt = "Create a concise summary (2-3 sentences) that describes what topics, concepts, and information this document contains. " +
            "Focus on what someone would find in this document, not what it means. " +
            "The summary should help with document retrieval and search.";

        private readonly string _systemPrompt;

        /// <summary>
        /// Initializes a new instance of the <see cref="SummarizationService"/> class.
        /// </summary>
        /// <param name="oneShotService">The one-shot service to use for summarization.</param>
        /// <param name="systemPrompt">An optional system prompt to guide the summary style.</param>
        public SummarizationService(IOneShotService oneShotService, string? systemPrompt = null) 
            : base(oneShotService)
        {
            _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
        }

        /// <summary>
        /// Generates a summary for the provided text using the underlying one-shot service.
        /// </summary>
        /// <param name="text">The text to summarize.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A summary of the input text.</returns>
        public Task<string> SummarizeAsync(string text, CancellationToken cancellationToken = default)
        {
            return ProcessAsync(_systemPrompt, text, cancellationToken);
        }
    }
} 