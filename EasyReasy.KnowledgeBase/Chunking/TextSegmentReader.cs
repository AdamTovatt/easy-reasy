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

        /// <summary>
        /// Initializes a new instance of the <see cref="TextSegmentReader"/> class.
        /// </summary>
        /// <param name="contentReader">The stream reader to read content from.</param>
        /// <param name="breakStrings">The strings that indicate break points, in order of preference.</param>
        public TextSegmentReader(StreamReader contentReader, params string[] breakStrings)
        {
            _contentReader = contentReader;
            _breakStrings = breakStrings;

            // Sort break strings by length (longest first) for more efficient matching
            Array.Sort(_breakStrings, (a, b) => b.Length.CompareTo(a.Length));
        }

        /// <summary>
        /// Reads the next text segment from the stream.
        /// </summary>
        /// <returns>The next text segment as a string, or null if no more content is available.</returns>
        public async Task<string?> ReadNextTextSegmentAsync()
        {
            if (_contentReader.EndOfStream)
                return null;

            StringBuilder segmentBuilder = new StringBuilder();
            char[] buffer = new char[1];

            while (await _contentReader.ReadAsync(buffer, 0, 1) > 0)
            {
                char currentChar = buffer[0];
                segmentBuilder.Append(currentChar);

                if (IsBreakPoint(segmentBuilder))
                {
                    return segmentBuilder.ToString();
                }
            }

            // Return any remaining content
            return segmentBuilder.Length > 0 ? segmentBuilder.ToString() : null;
        }

        private bool IsBreakPoint(StringBuilder content)
        {
            // Check each break string to see if our current content ends with it
            foreach (string breakString in _breakStrings)
            {
                if (EndsWith(content, breakString))
                {
                    return true;
                }
            }

            return false;
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