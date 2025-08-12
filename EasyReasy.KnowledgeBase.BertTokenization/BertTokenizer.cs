using EasyReasy.KnowledgeBase.Chunking;
using FastBertTokenizer;

namespace EasyReasy.KnowledgeBase.BertTokenization
{
    /// <summary>
    /// A BERT-based tokenizer implementation using the FastBertTokenizer library.
    /// </summary>
    public class BertTokenizer : ITokenizer
    {
        private readonly FastBertTokenizer.BertTokenizer _tokenizer;

        /// <summary>
        /// Max tokens to allow during encoding before truncation. Default is 2048.
        /// </summary>
        public int MaxEncodingTokens { get; set; } = 2048;

        /// <summary>
        /// Initializes a new instance of the <see cref="BertTokenizer"/> class with BERT base uncased vocabulary.
        /// </summary>
        private BertTokenizer()
        {
            _tokenizer = new FastBertTokenizer.BertTokenizer();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BertTokenizer"/> class with a custom tokenizer.
        /// </summary>
        /// <param name="tokenizer">The custom tokenizer instance.</param>
        private BertTokenizer(FastBertTokenizer.BertTokenizer tokenizer)
        {
            _tokenizer = tokenizer;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="BertTokenizer"/> class with BERT base uncased vocabulary.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the initialized tokenizer.</returns>
        public static async Task<BertTokenizer> CreateAsync()
        {
            BertTokenizer tokenizer = new BertTokenizer();
            await tokenizer._tokenizer.LoadFromHuggingFaceAsync("bert-base-uncased");
            return tokenizer;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="BertTokenizer"/> class with a custom tokenizer.
        /// </summary>
        /// <param name="tokenizer">The custom tokenizer instance.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the initialized tokenizer.</returns>
        public static async Task<BertTokenizer> CreateAsync(FastBertTokenizer.BertTokenizer tokenizer)
        {
            await tokenizer.LoadFromHuggingFaceAsync("bert-base-uncased");
            return new BertTokenizer(tokenizer);
        }

        /// <summary>
        /// Encodes a string into an array of tokens.
        /// </summary>
        /// <param name="text">The text to encode.</param>
        /// <returns>An array of tokens.</returns>
        public int[] Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<int>();

            // Encode with higher limit to avoid truncation
            (Memory<long> inputIds, _, _) = _tokenizer.Encode(text, MaxEncodingTokens);

            // Convert long[] to int[] - this assumes token IDs fit in int range
            long[] longTokens = inputIds.ToArray();
            int[] intTokens = new int[longTokens.Length];

            for (int i = 0; i < longTokens.Length; i++)
            {
                intTokens[i] = (int)longTokens[i];
            }

            return intTokens;
        }

        /// <summary>
        /// Decodes an array of tokens back into a string.
        /// </summary>
        /// <param name="tokens">The tokens to decode.</param>
        /// <returns>The decoded string.</returns>
        public string Decode(int[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
                return string.Empty;

            // Convert int[] to long[]
            long[] longTokens = new long[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                longTokens[i] = tokens[i];
            }

            return _tokenizer.Decode(longTokens);
        }

        /// <summary>
        /// Counts the number of tokens in a string.
        /// </summary>
        /// <param name="text">The text to count tokens for.</param>
        /// <returns>The number of tokens.</returns>
        public int CountTokens(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Encode with higher limit to avoid truncation
            (Memory<long> inputIds, _, _) = _tokenizer.Encode(text, MaxEncodingTokens);
            return inputIds.Length;
        }
    }
}