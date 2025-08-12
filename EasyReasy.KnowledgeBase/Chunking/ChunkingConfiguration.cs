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
        public int MaxTokensPerChunk { get; set; } = 300;

        /// <summary>
        /// Gets or sets the maximum number of tokens per section.
        /// </summary>
        public int MaxTokensPerSection { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the tokenizer to use for text processing.
        /// </summary>
        public ITokenizer Tokenizer { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChunkingConfiguration"/> class.
        /// </summary>
        /// <param name="tokenizer">The tokenizer to use for text processing.</param>
        /// <param name="maxTokensPerChunk">The maximum number of tokens per chunk.</param>
        /// <param name="maxTokensPerSection">The maximum number of tokens per section.</param>
        public ChunkingConfiguration(ITokenizer tokenizer, int maxTokensPerChunk = 300, int maxTokensPerSection = 10000)
        {
            Tokenizer = tokenizer;
            MaxTokensPerChunk = maxTokensPerChunk;
            MaxTokensPerSection = maxTokensPerSection;
        }
    }
}