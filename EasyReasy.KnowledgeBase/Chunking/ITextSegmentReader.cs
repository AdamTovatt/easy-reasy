namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Provides generic text segmentation capabilities for any text format.
    /// </summary>
    public interface ITextSegmentReader
    {
        /// <summary>
        /// Reads the next text segment from the stream.
        /// </summary>
        /// <returns>The next text segment as a string, or null if no more content is available.</returns>
        Task<string?> ReadNextTextSegmentAsync();
    }
}