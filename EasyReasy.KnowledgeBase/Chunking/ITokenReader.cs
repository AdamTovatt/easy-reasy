namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Provides streaming tokenization capabilities with forward and backward buffer support.
    /// </summary>
    public interface ITokenReader
    {
        /// <summary>
        /// Reads the next specified number of tokens from the stream.
        /// </summary>
        /// <param name="tokenCount">The number of tokens to read.</param>
        /// <returns>An array of tokens, or null if no more tokens are available.</returns>
        int[]? ReadNextTokens(int tokenCount);

        /// <summary>
        /// Peeks at the next specified number of tokens without consuming them.
        /// </summary>
        /// <param name="tokenCount">The number of tokens to peek at.</param>
        /// <returns>An array of tokens, or null if no more tokens are available.</returns>
        int[]? PeekNextTokens(int tokenCount);

        /// <summary>
        /// Seeks backward in the token buffer by the specified number of tokens.
        /// </summary>
        /// <param name="tokenCount">The number of tokens to seek backward.</param>
        /// <returns>True if the seek operation was successful, false if not enough tokens in buffer.</returns>
        bool SeekBackward(int tokenCount);

        /// <summary>
        /// Gets the current position in the token stream.
        /// </summary>
        int CurrentPosition { get; }

        /// <summary>
        /// Gets the total number of tokens read so far.
        /// </summary>
        int TotalTokensRead { get; }

        /// <summary>
        /// Checks if there are more tokens available to read.
        /// </summary>
        bool HasMoreTokens { get; }
    }
}