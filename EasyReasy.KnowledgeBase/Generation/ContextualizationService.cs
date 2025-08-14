namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Provides contextual information for text snippets within surrounding content using any underlying one-shot service.
    /// </summary>
    public class ContextualizationService : OneShotServiceBase, IContextualizationService
    {
        private const string DefaultSystemPrompt = "Provide contextual information about the given text snippet within its surrounding content. " +
            "Explain how the snippet relates to the broader context, what it refers to, and why it's important in that context. " +
            "Focus on helping someone understand the snippet's role and significance within the larger text. " +
            "Keep the response concise (2-4 sentences).";

        private readonly string _systemPrompt;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextualizationService"/> class.
        /// </summary>
        /// <param name="oneShotService">The one-shot service to use for contextualization.</param>
        /// <param name="systemPrompt">An optional system prompt to guide the contextualization style.</param>
        public ContextualizationService(IOneShotService oneShotService, string? systemPrompt = null)
            : base(oneShotService)
        {
            _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
        }

        /// <summary>
        /// Generates contextual information for a text snippet within surrounding content using the underlying one-shot service.
        /// </summary>
        /// <param name="textSnippet">The shorter piece of text to be contextualized.</param>
        /// <param name="surroundingContent">The surrounding content that provides context for the text snippet.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>Contextual information about the text snippet.</returns>
        public Task<string> ContextualizeAsync(string textSnippet, string surroundingContent, CancellationToken cancellationToken = default)
        {
            string userInput = $"Text snippet to contextualize:\n{textSnippet}\n\nSurrounding content:\n{surroundingContent}";
            return ProcessAsync(_systemPrompt, userInput, cancellationToken);
        }
    }
}