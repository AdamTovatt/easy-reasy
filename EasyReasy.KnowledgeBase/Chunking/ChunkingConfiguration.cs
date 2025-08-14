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
        /// Gets or sets the tokenizer to use for text processing.
        /// </summary>
        public ITokenizer Tokenizer { get; set; }

        /// <summary>
        /// Gets or sets the chunk stop signals that should cause chunking to stop.
        /// When a text segment starts with any of these signals, it will be excluded from the current chunk
        /// and will start the next chunk instead.
        /// </summary>
        public string[] ChunkStopSignals { get; set; } = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="ChunkingConfiguration"/> class.
        /// </summary>
        /// <param name="tokenizer">The tokenizer to use for text processing.</param>
        /// <param name="maxTokensPerChunk">The maximum number of tokens per chunk.</param>
        /// <param name="chunkStopSignals">Optional chunk stop signals that should cause chunking to stop.</param>
        public ChunkingConfiguration(ITokenizer tokenizer, int maxTokensPerChunk = 300, string[]? chunkStopSignals = null)
        {
            Tokenizer = tokenizer;
            MaxTokensPerChunk = maxTokensPerChunk;
            ChunkStopSignals = chunkStopSignals ?? [];
        }
    }
}