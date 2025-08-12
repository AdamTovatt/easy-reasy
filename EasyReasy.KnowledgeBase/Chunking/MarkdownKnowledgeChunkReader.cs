using System.Text;

namespace EasyReasy.KnowledgeBase.Chunking
{
    public class MarkdownKnowledgeChunkReader : IKnowledgeChunkReader
    {
        private static readonly char[] _splittingCharacters = { '.', '\n', '#' };

        private readonly StreamReader _contentReader;
        private string? _backlogContent = null;

        public int CharacterTarget { get; set; }

        public MarkdownKnowledgeChunkReader(StreamReader contentReader, int characterTarget)
        {
            _contentReader = contentReader;
            CharacterTarget = characterTarget;
        }

        public async Task<string?> ReadNextChunkContentAsync()
        {
            StringBuilder chunkBuilder = new StringBuilder();

            int currentCharacterCount = 0;

            while (currentCharacterCount < CharacterTarget)
            {
                string? line = await GetNextContentAsync();

                if (line == null)
                    break;

                chunkBuilder.Append(line);
                currentCharacterCount += line.Length;
            }

            int splitIndexOffset = await FindSuitableSplitIndexOffset(chunkBuilder);

            if (splitIndexOffset > 0) // we found a suitable split in forward buffer, take from that
            {
                if (_backlogContent != null)
                {
                    chunkBuilder.Append(_backlogContent.Substring(0, splitIndexOffset));
                    _backlogContent = _backlogContent.Substring(splitIndexOffset + 1);
                    // what happens if the splitIndexOffset is the same as the length of _backlogContent?
                }
            }
            else if (splitIndexOffset < 0) // we found a suitable split backwards, take from that
            {
                int splitIndex = chunkBuilder.Length + splitIndexOffset;
                _backlogContent += chunkBuilder.ToString().Substring(splitIndex);
                chunkBuilder.Remove(splitIndex, splitIndexOffset * -1);
            }

            return chunkBuilder.ToString();
        }

        private async Task<int> FindSuitableSplitIndexOffset(StringBuilder chunkBuilder)
        {
            int currentOffsetIndex = 0;
            int chunkBuilderLength = chunkBuilder.Length;

            string? forwardBufferContent = await GetNextContentAsync();
            _backlogContent = forwardBufferContent;
            int forwardBufferContentLength = forwardBufferContent?.Length ?? 0;

            bool didSkipLastCharacterCheck = false;

            while (true)
            {
                char? currentChar = null;

                if (currentOffsetIndex <= 0)
                {
                    int characterIndex = chunkBuilderLength - 1 + currentOffsetIndex;

                    if (characterIndex < 0)
                    {
                        didSkipLastCharacterCheck = true;
                    }
                    else
                    {
                        currentChar = chunkBuilder[characterIndex];
                    }
                }
                else
                {
                    if (forwardBufferContent != null && currentOffsetIndex < forwardBufferContentLength)
                    {
                        currentChar = forwardBufferContent[currentOffsetIndex];
                    }
                    else
                    {
                        if (currentChar == null && didSkipLastCharacterCheck)
                        {
                            currentOffsetIndex = 0;
                            break;
                        }
                    }
                }

                if (currentChar != null && IsSuitableSplittingCharacter(currentChar.Value))
                {
                    break;
                }
                else
                {
                    if (currentOffsetIndex <= 0)
                    {
                        currentOffsetIndex -= 1;
                    }

                    currentOffsetIndex *= -1;
                }
            }

            return currentOffsetIndex;
        }

        private bool IsSuitableSplittingCharacter(char character)
        {
            return _splittingCharacters.Contains(character);
        }

        private async Task<string?> GetNextContentAsync()
        {
            if (_backlogContent != null)
            {
                string result = _backlogContent;
                _backlogContent = null;
                return result;
            }
            else
            {
                return await _contentReader.ReadLineAsync();
            }
        }
    }
}
