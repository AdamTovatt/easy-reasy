using EasyReasy.KnowledgeBase.Generation;
using System.Text.Json;

namespace EasyReasy.KnowledgeBase.Tests.TestUtilities
{
    /// <summary>
    /// Test implementation of IEmbeddingService that stores embeddings in a dictionary
    /// and can serialize/deserialize them for persistent test scenarios.
    /// </summary>
    public sealed class PersistentEmbeddingService : IEmbeddingService
    {
        private readonly Dictionary<string, float[]> _embeddings;
        private readonly string _modelName;

        /// <summary>
        /// Gets the name of the embedding model used by this service.
        /// </summary>
        public string ModelName => _modelName;

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistentEmbeddingService"/> class.
        /// </summary>
        /// <param name="modelName">The name of the embedding model.</param>
        public PersistentEmbeddingService(string modelName)
        {
            _modelName = modelName;
            _embeddings = new Dictionary<string, float[]>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistentEmbeddingService"/> class with existing embeddings.
        /// </summary>
        /// <param name="modelName">The name of the embedding model.</param>
        /// <param name="embeddings">The initial embeddings dictionary.</param>
        public PersistentEmbeddingService(string modelName, Dictionary<string, float[]> embeddings)
        {
            _modelName = modelName;
            _embeddings = new Dictionary<string, float[]>(embeddings);
        }

        /// <summary>
        /// Generates an embedding vector for the specified text.
        /// Returns the stored embedding if available, otherwise throws an exception.
        /// </summary>
        /// <param name="text">The text to embed.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous embedding operation. The task result contains the embedding vector.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when no embedding is found for the specified text.</exception>
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            if (_embeddings.TryGetValue(text, out float[]? embedding))
            {
                return Task.FromResult(embedding);
            }

            throw new KeyNotFoundException($"No embedding found for text: {text}");
        }

        /// <summary>
        /// Adds an embedding for the specified text.
        /// </summary>
        /// <param name="text">The text to add an embedding for.</param>
        /// <param name="embedding">The embedding vector.</param>
        public void AddEmbedding(string text, float[] embedding)
        {
            _embeddings[text] = embedding;
        }

        /// <summary>
        /// Removes an embedding for the specified text.
        /// </summary>
        /// <param name="text">The text to remove the embedding for.</param>
        /// <returns>True if the embedding was removed; otherwise, false.</returns>
        public bool RemoveEmbedding(string text)
        {
            return _embeddings.Remove(text);
        }

        /// <summary>
        /// Checks if an embedding exists for the specified text.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True if an embedding exists; otherwise, false.</returns>
        public bool HasEmbedding(string text)
        {
            return _embeddings.ContainsKey(text);
        }

        /// <summary>
        /// Gets all stored text-embedding pairs.
        /// </summary>
        /// <returns>A dictionary containing all stored embeddings.</returns>
        public Dictionary<string, float[]> GetAllEmbeddings()
        {
            return new Dictionary<string, float[]>(_embeddings);
        }

        /// <summary>
        /// Clears all stored embeddings.
        /// </summary>
        public void Clear()
        {
            _embeddings.Clear();
        }

        /// <summary>
        /// Serializes the service to a stream.
        /// </summary>
        /// <returns>A stream containing the serialized embeddings.</returns>
        public Stream Serialize()
        {
            MemoryStream stream = new MemoryStream();
            var data = new
            {
                ModelName = _modelName,
                Embeddings = _embeddings,
            };
            JsonSerializer.Serialize(stream, data, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Serializes the service to a file.
        /// </summary>
        /// <param name="filePath">The path to the file where the service will be serialized.</param>
        public void Serialize(string filePath)
        {
            using FileStream fileStream = File.Create(filePath);
            var data = new
            {
                ModelName = _modelName,
                Embeddings = _embeddings,
            };
            JsonSerializer.Serialize(fileStream, data, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
        }

        /// <summary>
        /// Deserializes a service from a stream.
        /// </summary>
        /// <param name="stream">The stream containing serialized embeddings.</param>
        /// <returns>A new PersistentEmbeddingService instance with the deserialized embeddings.</returns>
        public static PersistentEmbeddingService Deserialize(Stream stream)
        {
            JsonElement data = JsonSerializer.Deserialize<JsonElement>(stream);
            if (!data.TryGetProperty("ModelName", out JsonElement modelNameElement) ||
                !data.TryGetProperty("Embeddings", out JsonElement embeddingsElement))
            {
                throw new InvalidOperationException("Invalid serialized data format.");
            }

            string modelName = modelNameElement.GetString() ?? throw new InvalidOperationException("Model name is null.");
            Dictionary<string, float[]>? embeddings = JsonSerializer.Deserialize<Dictionary<string, float[]>>(embeddingsElement.GetRawText());

            if (embeddings == null)
            {
                throw new InvalidOperationException("Failed to deserialize embeddings from stream.");
            }

            return new PersistentEmbeddingService(modelName, embeddings);
        }

        /// <summary>
        /// Deserializes a service from a file.
        /// </summary>
        /// <param name="filePath">The path to the file containing the serialized service.</param>
        /// <returns>A new PersistentEmbeddingService instance with the deserialized embeddings.</returns>
        public static PersistentEmbeddingService Deserialize(string filePath)
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return Deserialize(fileStream);
        }
    }
}
