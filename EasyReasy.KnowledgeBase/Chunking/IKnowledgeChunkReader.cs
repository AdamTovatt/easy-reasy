namespace EasyReasy.KnowledgeBase.Chunking
{
    public interface IKnowledgeChunkReader
    {
        /// <summary>
        /// Reads the next chunk of content from the stream.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The next chunk of content as a string, or null if no more content is available.</returns>
        public Task<string?> ReadNextChunkContentAsync(CancellationToken cancellationToken = default);
    }
}
