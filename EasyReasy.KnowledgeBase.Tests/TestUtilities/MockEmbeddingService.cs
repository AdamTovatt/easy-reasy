using EasyReasy.KnowledgeBase.Generation;

namespace EasyReasy.KnowledgeBase.Tests.TestUtilities
{
    /// <summary>
    /// Deterministic mock implementation of IEmbeddingService for tests.
    /// Generates normalized pseudo-random embeddings based on input text hash.
    /// </summary>
    public sealed class MockEmbeddingService : IEmbeddingService
    {
        /// <summary>
        /// Gets the name of the embedding model used by this service.
        /// </summary>
        public string ModelName => "mock-embedding-model";

        public int Dimensions => 384;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            float[] embedding = new float[Dimensions];

            int hash = text.GetHashCode();
            Random random = new Random(hash);

            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)((random.NextDouble() * 2) - 1);
            }

            float sumSquares = 0f;
            for (int i = 0; i < embedding.Length; i++)
            {
                sumSquares += embedding[i] * embedding[i];
            }

            float norm = (float)Math.Sqrt(sumSquares);
            if (norm > 0)
            {
                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] /= norm;
                }
            }

            return Task.FromResult(embedding);
        }
    }
}
