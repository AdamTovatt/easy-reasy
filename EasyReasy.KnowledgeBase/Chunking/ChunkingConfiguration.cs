namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Configuration for markdown chunking operations.
    /// </summary>
    public class ChunkingConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of tokens per chunk.
        /// </summary>
        public int MaxTokensPerChunk { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum number of tokens per section.
        /// </summary>
        public int MaxTokensPerSection { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the tokenizer to use for text processing.
        /// </summary>
        public ITokenizer Tokenizer { get; set; } = null!;
    }
} 