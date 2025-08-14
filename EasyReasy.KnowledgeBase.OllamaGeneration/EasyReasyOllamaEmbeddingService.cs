using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.Ollama.Client;
using EasyReasy.Ollama.Common;

namespace EasyReasy.KnowledgeBase.OllamaGeneration
{
    /// <summary>
    /// Ollama-based embedding service implementation.
    /// </summary>
    public sealed class EasyReasyOllamaEmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly OllamaClient _client;
        private readonly string _modelName;
        private bool _disposed = false;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyReasyOllamaEmbeddingService"/> class.
        /// </summary>
        /// <param name="client">The authorized Ollama client.</param>
        /// <param name="httpClient">The HTTP client to dispose.</param>
        /// <param name="modelName">The model name to use for embeddings.</param>
        private EasyReasyOllamaEmbeddingService(OllamaClient client, HttpClient httpClient, string modelName)
        {
            _client = client;
            _httpClient = httpClient;
            _modelName = modelName;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EasyReasyOllamaEmbeddingService"/> class with proper authentication.
        /// </summary>
        /// <param name="baseUrl">The base URL for the Ollama server.</param>
        /// <param name="apiKey">The API key for Ollama server authentication.</param>
        /// <param name="modelName">The model name to use for embeddings.</param>
        /// <param name="cancellationToken">Cancellation token for the creation operation.</param>
        /// <returns>A task that represents the asynchronous creation operation. The task result contains the initialized service.</returns>
        public static async Task<EasyReasyOllamaEmbeddingService> CreateAsync(
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

            return new EasyReasyOllamaEmbeddingService(client, httpClient, modelName);
        }

        /// <summary>
        /// Generates an embedding vector for the specified text using the Ollama API.
        /// </summary>
        /// <param name="text">The text to embed.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous embedding operation. The task result contains the embedding vector.</returns>
        public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EasyReasyOllamaEmbeddingService));

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty.", nameof(text));

            try
            {
                // Create the embedding request
                EmbeddingRequest request = new EmbeddingRequest(_modelName, text);

                // Get embeddings
                EmbeddingResponse response = await _client.Embeddings.GetEmbeddingsAsync(request, cancellationToken);

                return response.Embeddings;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to generate embedding for text: {exception.Message}", exception);
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
                _disposed = true;

                _httpClient.Dispose();
            }
        }
    }
}