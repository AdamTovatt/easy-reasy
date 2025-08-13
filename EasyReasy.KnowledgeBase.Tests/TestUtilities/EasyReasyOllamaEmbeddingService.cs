using EasyReasy.EnvironmentVariables;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.Ollama.Client;
using EasyReasy.Ollama.Common;

namespace EasyReasy.KnowledgeBase.Tests.TestUtilities
{
    /// <summary>
    /// Ollama-based embedding service implementation for integration testing.
    /// </summary>
    public sealed class EasyReasyOllamaEmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly OllamaClient _client;
        private readonly string _modelName;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="EasyReasyOllamaEmbeddingService"/> class.
        /// </summary>
        /// <param name="baseUrl">The base URL for the Ollama server.</param>
        /// <param name="apiKey">The API key for Ollama server authentication.</param>
        /// <param name="modelName">The model name to use for embeddings.</param>
        public EasyReasyOllamaEmbeddingService(string baseUrl, string apiKey, string modelName)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("Base URL cannot be null or empty.", nameof(baseUrl));

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty.", nameof(apiKey));

            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("Model name cannot be null or empty.", nameof(modelName));

            _modelName = modelName;

            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(baseUrl);

            // Create the client synchronously - we'll authorize it in the first call
            _client = OllamaClient.CreateUnauthorized(httpClient, apiKey);
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
                // Ensure the client is authorized and model is available
                await EnsureModelAvailableAsync(cancellationToken);

                // Create the embedding request
                EmbeddingRequest request = new EmbeddingRequest(_modelName, text);

                // Get embeddings
                EmbeddingResponse response = await _client.Embeddings.GetEmbeddingsAsync(request, cancellationToken);

                return response.Embeddings;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to generate embedding for text: {ex.Message}", ex);
            }
        }

        private async Task EnsureModelAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if the model is available
                bool isAvailable = await _client.IsModelAvailableAsync(_modelName, cancellationToken);
                if (!isAvailable)
                {
                    throw new InvalidOperationException($"Model '{_modelName}' is not available on the Ollama server.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to verify model availability: {ex.Message}", ex);
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
            }
        }
    }
}