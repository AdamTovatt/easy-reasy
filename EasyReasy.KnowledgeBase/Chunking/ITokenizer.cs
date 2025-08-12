namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// Provides methods for tokenizing and detokenizing text.
    /// </summary>
    public interface ITokenizer
    {
        /// <summary>
        /// Encodes a string into an array of tokens.
        /// </summary>
        /// <param name="text">The text to encode.</param>
        /// <returns>An array of tokens.</returns>
        int[] Encode(string text);

        /// <summary>
        /// Decodes an array of tokens back into a string.
        /// </summary>
        /// <param name="tokens">The tokens to decode.</param>
        /// <returns>The decoded string.</returns>
        string Decode(int[] tokens);

        /// <summary>
        /// Counts the number of tokens in a string.
        /// </summary>
        /// <param name="text">The text to count tokens for.</param>
        /// <returns>The number of tokens.</returns>
        int CountTokens(string text);
    }
}