namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Abstract base class for services that are built on top of IOneShotService.
    /// Provides a consistent foundation and reduces boilerplate code for implementers.
    /// </summary>
    public abstract class OneShotServiceBase
    {
        /// <summary>
        /// Gets the underlying one-shot service used for processing.
        /// </summary>
        protected IOneShotService OneShotService { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OneShotServiceBase"/> class.
        /// </summary>
        /// <param name="oneShotService">The one-shot service to use for processing.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="oneShotService"/> is null.</exception>
        protected OneShotServiceBase(IOneShotService oneShotService)
        {
            OneShotService = oneShotService ?? throw new ArgumentNullException(nameof(oneShotService));
        }

        /// <summary>
        /// Processes text using the underlying one-shot service with the specified system prompt.
        /// </summary>
        /// <param name="systemPrompt">The system prompt that defines the task and output format.</param>
        /// <param name="userInput">The input text to process.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous processing operation. The task result contains the processed output.</returns>
        protected Task<string> ProcessAsync(string systemPrompt, string userInput, CancellationToken cancellationToken = default)
        {
            return OneShotService.ProcessAsync(systemPrompt, userInput, cancellationToken);
        }
    }
}