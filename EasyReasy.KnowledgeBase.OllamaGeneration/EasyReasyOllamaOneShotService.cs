using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.Ollama.Client;
using EasyReasy.Ollama.Common;

namespace EasyReasy.KnowledgeBase.OllamaGeneration
{
    /// <summary>
    /// Ollama-based one-shot text processing service implementation.
    /// </summary>
    public sealed class EasyReasyOllamaOneShotService : IOneShotService, IDisposable
    {
        private readonly OllamaClient _client;
        private readonly string _modelName;
        private bool _disposed = false;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyReasyOllamaOneShotService"/> class.
        /// </summary>
        /// <param name="client">The authorized Ollama client.</param>
        /// <param name="httpClient">The HTTP client to dispose.</param>
        /// <param name="modelName">The model name to use for text processing.</param>
        private EasyReasyOllamaOneShotService(OllamaClient client, HttpClient httpClient, string modelName)
        {
            _client = client;
            _httpClient = httpClient;
            _modelName = modelName;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EasyReasyOllamaOneShotService"/> class with proper authentication.
        /// </summary>
        /// <param name="baseUrl">The base URL for the Ollama server.</param>
        /// <param name="apiKey">The API key for Ollama server authentication.</param>
        /// <param name="modelName">The model name to use for text processing.</param>
        /// <param name="cancellationToken">Cancellation token for the creation operation.</param>
        /// <returns>A task that represents the asynchronous creation operation. The task result contains the initialized service.</returns>
        public static async Task<EasyReasyOllamaOneShotService> CreateAsync(
            string baseUrl, 
            string apiKey, 
            string modelName, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("Base URL cannot be null or empty.", nameof(baseUrl));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty.", nameof(apiKey));

            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("Model name cannot be null or empty.", nameof(modelName));

            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(baseUrl);

            // Create an authenticated client as recommended by the documentation
            OllamaClient client = await OllamaClient.CreateAuthorizedAsync(httpClient, apiKey, cancellationToken);

            return new EasyReasyOllamaOneShotService(client, httpClient, modelName);
        }

        /// <summary>
        /// Performs a one-shot text processing task using the Ollama API.
        /// </summary>
        /// <param name="systemPrompt">The system prompt that defines the task and output format.</param>
        /// <param name="userInput">The input text to process.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous processing operation. The task result contains the processed output.</returns>
        public async Task<string> ProcessAsync(string systemPrompt, string userInput, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EasyReasyOllamaOneShotService));

            if (string.IsNullOrWhiteSpace(systemPrompt))
                throw new ArgumentException("System prompt cannot be null or empty.", nameof(systemPrompt));

            if (string.IsNullOrWhiteSpace(userInput))
                throw new ArgumentException("User input cannot be null or empty.", nameof(userInput));

            try
            {
                // Create the chat request with system and user messages
                List<Message> messages = new List<Message>
                {
                    new Message(ChatRole.System, systemPrompt),
                    new Message(ChatRole.User, userInput),
                };

                // Get the complete response (not streaming)
                await using IAsyncEnumerator<ChatResponsePart> enumerator = _client.Chat.StreamChatAsync(_modelName, messages, cancellationToken).GetAsyncEnumerator(cancellationToken);
                
                System.Text.StringBuilder responseBuilder = new System.Text.StringBuilder();
                while (await enumerator.MoveNextAsync())
                {
                    ChatResponsePart part = enumerator.Current;
                    if (part.Message != null)
                    {
                        responseBuilder.Append(part.Message);
                    }
                }

                return responseBuilder.ToString();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to process text with one-shot service: {exception.Message}", exception);
            }
        }

        /// <summary>
        /// Disposes the underlying HTTP client and resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _client?.Dispose();
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}