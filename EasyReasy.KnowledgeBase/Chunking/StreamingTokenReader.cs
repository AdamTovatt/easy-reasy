using System.Collections.Generic;

namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// A streaming token reader that maintains forward and backward buffers for efficient token processing.
    /// </summary>
    public class StreamingTokenReader : ITokenReader
    {
        private readonly StreamReader _contentReader;
        private readonly ITokenizer _tokenizer;
        private readonly Queue<int> _forwardBuffer;
        private readonly Stack<int> _backwardBuffer;
        private readonly int _maxBufferSize;

        private int _currentPosition;
        private int _totalTokensRead;
        private bool _endOfStream;

        /// <summary>
        /// Gets the current position in the token stream.
        /// </summary>
        public int CurrentPosition => _currentPosition;

        /// <summary>
        /// Gets the total number of tokens read so far.
        /// </summary>
        public int TotalTokensRead => _totalTokensRead;

        /// <summary>
        /// Gets a value indicating whether there are more tokens available to read.
        /// </summary>
        public bool HasMoreTokens => !_endOfStream || _forwardBuffer.Count > 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingTokenReader"/> class.
        /// </summary>
        /// <param name="contentReader">The stream reader to read content from.</param>
        /// <param name="tokenizer">The tokenizer to use for encoding text.</param>
        /// <param name="maxBufferSize">The maximum size of the forward buffer. Defaults to 10000.</param>
        public StreamingTokenReader(StreamReader contentReader, ITokenizer tokenizer, int maxBufferSize = 10000)
        {
            _contentReader = contentReader;
            _tokenizer = tokenizer;
            _maxBufferSize = maxBufferSize;
            _forwardBuffer = new Queue<int>();
            _backwardBuffer = new Stack<int>();
            _currentPosition = 0;
            _totalTokensRead = 0;
            _endOfStream = false;
        }

        /// <summary>
        /// Reads the next specified number of tokens from the stream.
        /// </summary>
        /// <param name="tokenCount">The number of tokens to read.</param>
        /// <returns>An array of tokens, or null if no more tokens are available.</returns>
        public int[]? ReadNextTokens(int tokenCount)
        {
            if (tokenCount <= 0)
                return Array.Empty<int>();

            List<int> result = new List<int>();

            // First, try to get tokens from the forward buffer
            while (result.Count < tokenCount && _forwardBuffer.Count > 0)
            {
                int token = _forwardBuffer.Dequeue();
                result.Add(token);
                _currentPosition++;
            }

            // If we still need more tokens, read from the stream
            while (result.Count < tokenCount && !_endOfStream)
            {
                string? line = _contentReader.ReadLine();
                if (line == null)
                {
                    _endOfStream = true;
                    break;
                }

                int[] lineTokens = _tokenizer.Encode(line);
                
                // Add newline token if not the last line
                if (!_endOfStream)
                {
                    int[] newlineTokens = _tokenizer.Encode("\n");
                    int[] combinedTokens = new int[lineTokens.Length + newlineTokens.Length];
                    Array.Copy(lineTokens, 0, combinedTokens, 0, lineTokens.Length);
                    Array.Copy(newlineTokens, 0, combinedTokens, lineTokens.Length, newlineTokens.Length);
                    lineTokens = combinedTokens;
                }

                foreach (int token in lineTokens)
                {
                    if (result.Count < tokenCount)
                    {
                        result.Add(token);
                        _currentPosition++;
                    }
                    else
                    {
                        // Add remaining tokens to forward buffer
                        _forwardBuffer.Enqueue(token);
                        if (_forwardBuffer.Count > _maxBufferSize)
                        {
                            _forwardBuffer.Dequeue(); // Remove oldest token
                        }
                    }
                }

                _totalTokensRead += lineTokens.Length;
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        /// <summary>
        /// Peeks at the next specified number of tokens without consuming them.
        /// </summary>
        /// <param name="tokenCount">The number of tokens to peek at.</param>
        /// <returns>An array of tokens, or null if no more tokens are available.</returns>
        public int[]? PeekNextTokens(int tokenCount)
        {
            if (tokenCount <= 0)
                return Array.Empty<int>();

            List<int> result = new List<int>();

            // First, get tokens from the forward buffer
            foreach (int token in _forwardBuffer)
            {
                if (result.Count >= tokenCount)
                    break;
                result.Add(token);
            }

            // If we still need more tokens, we need to read them but not consume them
            if (result.Count < tokenCount && !_endOfStream)
            {
                // This is a simplified implementation - in practice, we might need to cache more
                // For now, we'll just return what we can peek without reading more
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        /// <summary>
        /// Seeks backward in the token buffer by the specified number of tokens.
        /// </summary>
        /// <param name="tokenCount">The number of tokens to seek backward.</param>
        /// <returns>True if the seek operation was successful, false if not enough tokens in buffer.</returns>
        public bool SeekBackward(int tokenCount)
        {
            if (tokenCount <= 0)
                return true;

            if (tokenCount > _backwardBuffer.Count)
                return false;

            // Move tokens from backward buffer to forward buffer
            for (int i = 0; i < tokenCount; i++)
            {
                int token = _backwardBuffer.Pop();
                _forwardBuffer.Enqueue(token);
                _currentPosition--;
            }

            return true;
        }
    }
} 