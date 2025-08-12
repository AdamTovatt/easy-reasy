namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// A knowledge chunk reader that processes markdown content using token-based chunking.
    /// </summary>
    public class MarkdownKnowledgeChunkReader : IKnowledgeChunkReader
    {
        private readonly ITextSegmentReader _textSegmentReader;
        private readonly ChunkingConfiguration _configuration;
        private readonly ITokenizer _tokenizer;
        private string? _bufferedChunk;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkdownKnowledgeChunkReader"/> class.
        /// </summary>
        /// <param name="contentReader">The stream reader to read markdown content from.</param>
        /// <param name="configuration">The configuration for chunking operations.</param>
        public MarkdownKnowledgeChunkReader(StreamReader contentReader, ChunkingConfiguration configuration)
        {
            _configuration = configuration;
            _tokenizer = configuration.Tokenizer;

            // Create a text segment reader configured for markdown with appropriate break strings
            _textSegmentReader = new TextSegmentReader(
                contentReader,
                "\n\n# ",    // New top-level heading
                "\n## ",     // New second-level heading
                "\n### ",    // New third-level heading
                "\n#### ",   // New fourth-level heading
                "\n##### ",  // New fifth-level heading
                "\n###### ", // New sixth-level heading
                "\n\n",      // Double line breaks (paragraph breaks)
                "\n- ",      // Unordered list items
                "\n* ",      // Alternative unordered list items
                "\n+ ",      // Alternative unordered list items
                "\n1. ",     // Ordered list items (simplified - just checking for "1.")
                "\n```\n",   // End of code blocks
                "\n",        // Single line breaks
                ". ",        // Sentence endings
                "! ",        // Exclamation marks
                "? "         // Question marks
            );

            _bufferedChunk = null;
        }

        /// <summary>
        /// Reads the next chunk of content from the markdown stream.
        /// </summary>
        /// <returns>The next chunk of content as a string, or null if no more content is available.</returns>
        public async Task<string?> ReadNextChunkContentAsync()
        {
            // If we have a buffered chunk, start with it
            string currentChunk = _bufferedChunk ?? string.Empty;
            _bufferedChunk = null;

            // If we don't have any buffered content, read the first segment
            if (string.IsNullOrEmpty(currentChunk))
            {
                string? firstSegment = await _textSegmentReader.ReadNextTextSegmentAsync();
                if (firstSegment == null)
                    return null;

                currentChunk = firstSegment;
            }

            // Check if the current chunk is already at or over the token limit
            int currentTokens = _tokenizer.CountTokens(currentChunk);
            if (currentTokens >= _configuration.MaxTokensPerChunk)
            {
                // Current chunk is already at or over the limit, return it as-is
                return currentChunk;
            }

            // Keep reading segments and adding them to the chunk until we reach the token limit
            while (true)
            {
                string? nextSegment = await _textSegmentReader.ReadNextTextSegmentAsync();
                if (nextSegment == null)
                {
                    // No more content available, return what we have
                    return string.IsNullOrEmpty(currentChunk) ? null : currentChunk;
                }

                // Check if adding this segment would exceed the token limit
                string potentialChunk = currentChunk + nextSegment;
                int potentialTokens = _tokenizer.CountTokens(potentialChunk);

                if (potentialTokens <= _configuration.MaxTokensPerChunk)
                {
                    // Adding this segment keeps us within the limit, add it to the current chunk
                    currentChunk = potentialChunk;
                }
                else
                {
                    // Adding this segment would exceed the limit
                    // Buffer it for the next chunk and return the current chunk
                    _bufferedChunk = nextSegment;
                    return currentChunk;
                }
            }
        }
    }
}
