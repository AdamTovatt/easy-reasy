using System.Text;

namespace EasyReasy.KnowledgeBase.Chunking
{
    /// <summary>
    /// A generic text segment reader that can be configured for any text format.
    /// </summary>
    public class TextSegmentReader : ITextSegmentReader
    {
        private readonly StreamReader _contentReader;
        private readonly string[] _breakStrings;
        private readonly Queue<char> _unreadBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextSegmentReader"/> class.
        /// </summary>
        /// <param name="contentReader">The stream reader to read content from.</param>
        /// <param name="breakStrings">The strings that indicate break points, in order of preference.</param>
        private TextSegmentReader(StreamReader contentReader, params string[] breakStrings)
        {
            _contentReader = contentReader;
            _breakStrings = breakStrings;
            _unreadBuffer = new Queue<char>();

            // Sort break strings by length (longest first) for more efficient matching
            Array.Sort(_breakStrings, (a, b) => b.Length.CompareTo(a.Length));
        }

        /// <summary>
        /// Creates a new instance of <see cref="TextSegmentReader"/> with custom break strings.
        /// </summary>
        /// <param name="contentReader">The stream reader to read content from.</param>
        /// <param name="breakStrings">The strings that indicate break points, in order of preference.</param>
        /// <returns>A new instance of <see cref="TextSegmentReader"/> configured with the specified break strings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentReader"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="breakStrings"/> is null or empty.</exception>
        public static TextSegmentReader Create(StreamReader contentReader, params string[] breakStrings)
        {
            if (contentReader == null)
                throw new ArgumentNullException(nameof(contentReader), "Content reader cannot be null.");
            if (breakStrings == null || breakStrings.Length == 0)
                throw new ArgumentException("At least one break string must be provided.", nameof(breakStrings));
            return new TextSegmentReader(contentReader, breakStrings);
        }

        /// <summary>
        /// Creates a new instance of <see cref="TextSegmentReader"/> pre-configured for Markdown content.
        /// </summary>
        /// <param name="contentReader">The stream reader to read content from.</param>
        /// <returns>A new instance of <see cref="TextSegmentReader"/> configured with Markdown break strings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentReader"/> is null.</exception>
        public static TextSegmentReader CreateForMarkdown(StreamReader contentReader)
        {
            if (contentReader == null)
                throw new ArgumentNullException(nameof(contentReader), "Content reader cannot be null.");
            return new TextSegmentReader(contentReader, TextSegmentSplitters.Markdown);
        }

        /// <summary>
        /// Reads the next text segment from the stream.
        /// </summary>
        /// <returns>The next text segment as a string, or null if no more content is available.</returns>
        public async Task<string?> ReadNextTextSegmentAsync()
        {
            if (_contentReader.EndOfStream && _unreadBuffer.Count == 0)
                return null;

            StringBuilder segmentBuilder = new StringBuilder();
            string? currentMatch = null;
            int matchEndPosition = -1;

            while (true)
            {
                char currentChar;

                // First check our internal buffer
                if (_unreadBuffer.Count > 0)
                {
                    currentChar = _unreadBuffer.Dequeue();
                }
                else
                {
                    // Read from stream
                    char[] buffer = new char[1];
                    if (await _contentReader.ReadAsync(buffer, 0, 1) == 0)
                        break; // End of stream
                    currentChar = buffer[0];
                }

                segmentBuilder.Append(currentChar);

                string? longestMatch = FindLongestBreakString(segmentBuilder);

                if (longestMatch != null)
                {
                    // We found a match - record it but keep reading to see if we can find a longer one
                    currentMatch = longestMatch;
                    matchEndPosition = segmentBuilder.Length;
                }
                else if (currentMatch != null)
                {
                    // We had a match before but now we don't - the previous match was the longest
                    string result = segmentBuilder.ToString().Substring(0, matchEndPosition);

                    // Put back the characters we read beyond the match
                    string extraChars = segmentBuilder.ToString().Substring(matchEndPosition);
                    foreach (char c in extraChars)
                    {
                        _unreadBuffer.Enqueue(c);
                    }

                    return result;
                }
            }

            // Handle end of stream cases
            if (currentMatch != null)
            {
                // We had a pending match - return it
                return segmentBuilder.ToString().Substring(0, matchEndPosition);
            }

            // No matches found, return all content
            return segmentBuilder.Length > 0 ? segmentBuilder.ToString() : null;
        }

        private string? FindLongestBreakString(StringBuilder content)
        {
            // Find the longest break string that matches at the end of our content
            // Since break strings are sorted by length (longest first), the first match is the longest
            foreach (string breakString in _breakStrings)
            {
                if (EndsWith(content, breakString))
                {
                    return breakString;
                }
            }

            return null;
        }

        private bool EndsWith(StringBuilder content, string value)
        {
            if (value.Length > content.Length)
                return false;

            // Check if the last 'value.Length' characters match the break string
            for (int i = 0; i < value.Length; i++)
            {
                if (content[content.Length - value.Length + i] != value[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}