using EasyReasy.KnowledgeBase.BertTokenization;
using EasyReasy.KnowledgeBase.Chunking;
using System.Reflection;

namespace EasyReasy.KnowledgeBase.Tests.Chunking
{
    [TestClass]
    public class SegmentBasedChunkReaderMarkdownTests
    {
        private static ResourceManager _resourceManager = null!;
        private static ITokenizer _tokenizer = null!;

        [ClassInitialize]
        public static async Task BeforeAll(TestContext testContext)
        {
            _resourceManager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly());
            _tokenizer = await BertTokenizer.CreateAsync();
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldReturnNull_WhenNoContent()
        {
            // Arrange
            string content = "";
            Console.WriteLine($"Original content: \"{content}\"");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 100);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            string? result = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Result should be null when no content is provided");
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldReturnSingleSegment_WhenUnderTokenLimit()
        {
            // Arrange
            string content = "# Test Heading\n\nThis is a simple paragraph.";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 100);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            string? result = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, "Result should not be null when content is provided");
            Console.WriteLine($"Created chunk:\n{result}");
            Assert.IsTrue(result.Contains("# Test Heading"), $"Chunk should contain heading. Chunk content: {result}");
            Assert.IsTrue(result.Contains("This is a simple paragraph."), $"Chunk should contain paragraph. Chunk content: {result}");
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldCombineSegments_WhenUnderTokenLimit()
        {
            // Arrange
            string content = "# Test Heading\n\nThis is paragraph one.\n\nThis is paragraph two.";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 200);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            string? result = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, "Result should not be null when content is provided");
            Console.WriteLine($"Created chunk:\n{result}");
            Assert.IsTrue(result.Contains("# Test Heading"), $"Chunk should contain heading. Chunk content: {result}");
            Assert.IsTrue(result.Contains("This is paragraph one."), $"Chunk should contain first paragraph. Chunk content: {result}");
            Assert.IsTrue(result.Contains("This is paragraph two."), $"Chunk should contain second paragraph. Chunk content: {result}");
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldRespectTokenLimit_WhenCombiningSegments()
        {
            // Arrange
            string content = "# Test Heading\n\nThis is paragraph one.\n\nThis two.\n\nThis three.";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 10);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            string? firstChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);
            string? secondChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);

            // Assert
            Assert.IsNotNull(firstChunk, "First chunk should not be null");
            Console.WriteLine($"\nFirst chunk:\n{firstChunk}");
            Assert.IsNotNull(secondChunk, "Second chunk should not be null");
            Console.WriteLine($"\nSecond chunk:\n{secondChunk}");
            Assert.IsTrue(firstChunk.Contains("# Test Heading"), $"First chunk should contain heading. Chunk content: {firstChunk}");
            Assert.IsTrue(firstChunk.Contains("This is paragraph one."), $"First chunk should contain first paragraph. Chunk content: {firstChunk}");
            Assert.IsTrue(secondChunk.Contains("This two."), $"Second chunk should contain second paragraph. Chunk content: {secondChunk}");
            Assert.IsTrue(secondChunk.Contains("This three."), $"Second chunk should contain third paragraph. Chunk content: {secondChunk}");
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldBreakAtMarkdownHeadings()
        {
            // Arrange
            string content = await _resourceManager.ReadAsStringAsync(TestDataFiles.TestDocument02);
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 100, chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            string? firstChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);
            string? secondChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);
            string? thirdChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);

            // Assert
            Assert.IsNotNull(firstChunk, "First chunk should not be null");
            Console.WriteLine($"\nFirst chunk:\n{firstChunk}");
            Assert.IsNotNull(secondChunk, "Second chunk should not be null");
            Console.WriteLine($"\nSecond chunk:\n{secondChunk}");

            Assert.IsTrue(firstChunk.StartsWith("# First header"));
            Assert.IsTrue(secondChunk.StartsWith("## This is the second header"));
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldBreakAtListItems()
        {
            // Arrange
            string content = "# Test List\n\n- First item\n- Second item\n- Third item";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 10);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            string? firstChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);
            string? secondChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);

            // Assert
            Assert.IsNotNull(firstChunk, "First chunk should not be null");
            Assert.IsNotNull(secondChunk, "Second chunk should not be null");
            Console.WriteLine($"First chunk:\n{firstChunk}");
            Console.WriteLine($"Second chunk:\n{secondChunk}");
            Assert.IsTrue(firstChunk.Contains("# Test List"), $"First chunk should contain list heading. Chunk content: {firstChunk}");
            Assert.IsTrue(firstChunk.Contains("- First item"), $"First chunk should contain first list item. Chunk content: {firstChunk}");
            Assert.IsTrue(secondChunk.Contains("- Second item"), $"Second chunk should contain second list item. Chunk content: {secondChunk}");
            Assert.IsTrue(secondChunk.Contains("- Third item"), $"Second chunk should contain third list item. Chunk content: {secondChunk}");
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldBreakAtCustomStopSignals()
        {
            // Arrange
            string content = "First paragraph.\n\n**Bold text**\n\nSecond paragraph.\n\n```code block```\n\nThird paragraph.";
            Console.WriteLine($"Original content:\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            string[] customStopSignals = ["**", "```"];
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 50, chunkStopSignals: customStopSignals);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            string? firstChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);
            string? secondChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);
            string? thirdChunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None);

            // Assert
            Assert.IsNotNull(firstChunk, "First chunk should not be null");
            Assert.IsNotNull(secondChunk, "Second chunk should not be null");
            Assert.IsNotNull(thirdChunk, "Third chunk should not be null");
            
            Console.WriteLine($"First chunk:\n{firstChunk}");
            Console.WriteLine($"Second chunk:\n{secondChunk}");
            Console.WriteLine($"Third chunk:\n{thirdChunk}");
            
            Assert.IsTrue(firstChunk.Contains("First paragraph."), $"First chunk should contain first paragraph. Chunk content: {firstChunk}");
            Assert.IsTrue(secondChunk.StartsWith("**Bold text**"), $"Second chunk should start with bold text. Chunk content: {secondChunk}");
            Assert.IsTrue(thirdChunk.StartsWith("```code block```"), $"Third chunk should start with code block. Chunk content: {thirdChunk}");
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldHandleTestDocument01_WithRoundTrip()
        {
            // Arrange
            string content = await _resourceManager.ReadAsStringAsync(TestDataFiles.TestDocument01);
            Console.WriteLine($"Original content (length: {content.Length}):\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 100);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            List<string> chunks = new List<string>();
            string? chunk;
            while ((chunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None)) != null)
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.IsTrue(chunks.Count > 0, "Should have read at least one chunk");
            Assert.IsTrue(chunks.All(c => !string.IsNullOrEmpty(c)), "All chunks should be non-empty");

            Console.WriteLine($"Created {chunks.Count} chunks:");
            for (int i = 0; i < chunks.Count; i++)
            {
                Console.WriteLine($"Chunk {i + 1} (length: {chunks[i].Length}):\n{chunks[i]}");
            }

            // Verify the content is preserved
            string reconstructedContent = string.Concat(chunks);
            Console.WriteLine($"Reconstructed content (length: {reconstructedContent.Length}):\n{reconstructedContent}");
            Assert.AreEqual(content, reconstructedContent, "Reconstructed content should match original");
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_RoundTrip_ShouldPreserveOriginalContent()
        {
            // Arrange
            string originalContent = "# Test Document\n\nThis is the first paragraph.\n\n## Second Section\n\nThis is the second paragraph. It has multiple sentences. Each sentence ends with a period.\n\n### Subsection\n\nThis is the third paragraph with different punctuation! Some sentences end with exclamation marks? Others end with question marks.\n\n- List item one\n- List item two\n- List item three\n\nFinal paragraph without trailing newline.";

            Console.WriteLine($"Original content (length: {originalContent.Length}):\n{originalContent}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(originalContent)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 150);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act - Read all chunks
            List<string> chunks = new List<string>();
            string? chunk;
            while ((chunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None)) != null)
            {
                chunks.Add(chunk);
            }

            Console.WriteLine($"Created {chunks.Count} chunks:");
            for (int i = 0; i < chunks.Count; i++)
            {
                Console.WriteLine($"Chunk {i + 1} (length: {chunks[i].Length}):\n{chunks[i]}");
            }

            // Join all chunks back together
            string reconstructedContent = string.Concat(chunks);
            Console.WriteLine($"Reconstructed content (length: {reconstructedContent.Length}):\n{reconstructedContent}");

            // Assert
            Assert.IsTrue(chunks.Count > 0, "Should have read at least one chunk");
            Assert.AreEqual(originalContent, reconstructedContent, "Reconstructed content should exactly match the original content");
            Assert.AreEqual(originalContent.Length, reconstructedContent.Length, "Reconstructed content should have the same length as original");
        }

        [TestMethod]
        public async Task ReadNextChunkContentAsync_ShouldRespectTokenLimits_AcrossMultipleChunks()
        {
            // Arrange
            string content = "# Test Document\n\n" +
                           "This is paragraph one. " + new string('x', 50) + ".\n\n" +
                           "This is paragraph two. " + new string('y', 50) + ".\n\n" +
                           "This is paragraph three. " + new string('z', 50) + ".";

            Console.WriteLine($"Original content (length: {content.Length}):\n{content}");
            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration configuration = new ChunkingConfiguration(_tokenizer, 80);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, configuration);

            // Act
            List<string> chunks = new List<string>();
            string? chunk;
            while ((chunk = await chunkReader.ReadNextChunkContentAsync(CancellationToken.None)) != null)
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.IsTrue(chunks.Count > 1, "Should have created multiple chunks due to token limits");

            Console.WriteLine($"Created {chunks.Count} chunks:");
            for (int i = 0; i < chunks.Count; i++)
            {
                int tokenCount = _tokenizer.CountTokens(chunks[i]);
                Console.WriteLine($"Chunk {i + 1} (length: {chunks[i].Length}, tokens: {tokenCount}):\n{chunks[i]}");
            }

            // Verify each chunk is within token limits
            foreach (string chunkContent in chunks)
            {
                int tokenCount = _tokenizer.CountTokens(chunkContent);
                Assert.IsTrue(tokenCount <= configuration.MaxTokensPerChunk,
                    $"Chunk exceeds token limit. Tokens: {tokenCount}, Limit: {configuration.MaxTokensPerChunk}. Content: {chunkContent}");
            }
        }
    }
}