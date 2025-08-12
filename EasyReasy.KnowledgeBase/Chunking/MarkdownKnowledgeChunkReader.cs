using System.Text;

namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// A knowledge chunk reader that processes markdown content using token-based chunking.
    /// </summary>
    public class MarkdownKnowledgeChunkReader : IKnowledgeChunkReader
    {
        private readonly ITokenReader _tokenReader;
        private readonly ChunkingConfiguration _configuration;
        private readonly ITokenizer _tokenizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkdownKnowledgeChunkReader"/> class.
        /// </summary>
        /// <param name="contentReader">The stream reader to read markdown content from.</param>
        /// <param name="configuration">The configuration for chunking operations.</param>
        public MarkdownKnowledgeChunkReader(StreamReader contentReader, ChunkingConfiguration configuration)
        {
            _configuration = configuration;
            _tokenizer = configuration.Tokenizer;
            _tokenReader = new StreamingTokenReader(contentReader, _tokenizer);
        }

        /// <summary>
        /// Reads the next chunk of content from the markdown stream.
        /// </summary>
        /// <returns>The next chunk of content as a string, or null if no more content is available.</returns>
        public Task<string?> ReadNextChunkContentAsync()
        {
            List<int> chunkTokens = new List<int>();
            int currentTokenCount = 0;
            int sectionTokenCount = 0;

            while (currentTokenCount < _configuration.MaxTokensPerChunk && 
                   sectionTokenCount < _configuration.MaxTokensPerSection &&
                   _tokenReader.HasMoreTokens)
            {
                // Read tokens up to our target
                int tokensToRead = Math.Min(
                    _configuration.MaxTokensPerChunk - currentTokenCount,
                    _configuration.MaxTokensPerSection - sectionTokenCount
                );

                int[]? tokens = _tokenReader.ReadNextTokens(tokensToRead);
                if (tokens == null || tokens.Length == 0)
                    break;

                // Check if we've hit a header (markdown headers start with #)
                int headerSplitIndex = FindHeaderSplitPoint(tokens);
                
                if (headerSplitIndex >= 0)
                {
                    // We found a header, split here
                    for (int i = 0; i < headerSplitIndex; i++)
                    {
                        chunkTokens.Add(tokens[i]);
                        currentTokenCount++;
                        sectionTokenCount++;
                    }
                    
                    // Put the header and remaining tokens back in the buffer
                    int tokensToPutBack = tokens.Length - headerSplitIndex;
                    if (tokensToPutBack > 0)
                    {
                        // This is a simplified approach - in practice we'd need to handle this better
                        // For now, we'll just stop here and return what we have
                    }
                    break;
                }

                // No header found, add all tokens
                chunkTokens.AddRange(tokens);
                currentTokenCount += tokens.Length;
                sectionTokenCount += tokens.Length;

                // Look for other good splitting points (line breaks, periods)
                int splitIndex = FindGoodSplitPoint(tokens);
                if (splitIndex >= 0)
                {
                    // Found a good split point, truncate the chunk
                    chunkTokens.RemoveRange(chunkTokens.Count - tokens.Length + splitIndex, tokens.Length - splitIndex);
                    currentTokenCount -= (tokens.Length - splitIndex);
                    sectionTokenCount -= (tokens.Length - splitIndex);
                    
                    // Put remaining tokens back
                    int tokensToPutBack = tokens.Length - splitIndex;
                    if (tokensToPutBack > 0)
                    {
                        // Simplified approach - in practice we'd need to handle this better
                    }
                    break;
                }
            }

            if (chunkTokens.Count == 0)
                return Task.FromResult<string?>(null);

            // Convert tokens back to text
            return Task.FromResult<string?>(_tokenizer.Decode(chunkTokens.ToArray()));
        }

        private int FindHeaderSplitPoint(int[] tokens)
        {
            // This is a simplified implementation
            // In practice, we'd need to decode tokens and check for markdown header patterns
            // For now, we'll return -1 to indicate no header found
            return -1;
        }

        private int FindGoodSplitPoint(int[] tokens)
        {
            // This is a simplified implementation
            // In practice, we'd need to decode tokens and look for line breaks and periods
            // For now, we'll return -1 to indicate no good split point found
            return -1;
        }
    }
}
