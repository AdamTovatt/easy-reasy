using System;
using System.Threading;
using System.Threading.Tasks;
using EasyReasy.KnowledgeBase.Generation;

namespace EasyReasy.KnowledgeBase.Tests.TestUtilities
{
    /// <summary>
    /// Ollama-based embedding service implementation for integration testing.
    /// </summary>
    public sealed class EasyReasyOllamaEmbeddingService : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Ollama embedding service not yet implemented");
        }
    }
} 