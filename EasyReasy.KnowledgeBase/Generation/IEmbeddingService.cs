namespace EasyReasy.KnowledgeBase.Generation
{
    /// <summary>
    /// Service for generating embeddings from text content.
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generates an embedding vector for the specified text.
        /// </summary>
        /// <param name="text">The text to embed.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous embedding operation. The task result contains the embedding vector.</returns>
        Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    }
}