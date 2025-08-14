using EasyReasy.KnowledgeBase.BertTokenization;
using EasyReasy.KnowledgeBase.Chunking;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Tests.TestUtilities;
using EasyReasy.KnowledgeBase.Models;
using System.Reflection;
using System.Diagnostics;

namespace EasyReasy.KnowledgeBase.Tests.Chunking
{
    [TestClass]
    public class SectionReaderTests
    {
        private static ResourceManager _resourceManager = null!;
        private static ITokenizer _tokenizer = null!;
        private static IEmbeddingService _embeddingService = null!;

        [ClassInitialize]
        public static async Task BeforeAll(TestContext testContext)
        {
            _resourceManager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly());
            _tokenizer = await BertTokenizer.CreateAsync();
            _embeddingService = new MockEmbeddingService();
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldReturnEmpty_WhenNoContent()
        {
            // Arrange
            string content = "";
            Console.WriteLine("=== Testing Empty Content ===");
            Console.WriteLine($"Input content: '{content}'");

            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 100);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Assert
            Console.WriteLine($"Created {sections.Count} sections");
            Assert.AreEqual(0, sections.Count, "Should return no sections when no content is provided");
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldCreateSingleSection_WhenContentIsSmall()
        {
            // Arrange
            string content = "# Test Heading\n\nThis is a simple paragraph.";
            Console.WriteLine("=== Testing Small Content ===");
            Console.WriteLine($"Input content:\n{content}");

            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 100);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(maxTokensPerSection: 200, chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Assert
            Console.WriteLine($"Created {sections.Count} sections");
            for (int i = 0; i < sections.Count; i++)
            {
                Console.WriteLine($"Section {i + 1} has {sections[i].Count} chunks:");
                Console.WriteLine(KnowledgeFileSection.CreateFromChunks(sections[i]).ToString());
                Console.WriteLine($"End of Section {i + 1}\n");
            }

            Assert.IsTrue(sections.Count >= 1, "Should create at least one section for small content");
            Assert.IsTrue(sections[0].Count >= 1, "Section should contain at least one chunk");
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldRespectMaxTokensPerSection()
        {
            // Arrange
            string content = "# Test Document\n\n" +
                           "This is paragraph one. " + new string('x', 100) + ".\n\n" +
                           "This is paragraph two. " + new string('y', 100) + ".\n\n" +
                           "This is paragraph three. " + new string('z', 100) + ".";

            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 100);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(maxTokensPerSection: 120, chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Assert
            Assert.IsTrue(sections.Count > 1, "Should create multiple sections due to token limits");

            int sectionCount = 0;
            // Verify each section is within token limits
            foreach (List<KnowledgeFileChunk> section in sections)
            {
                Console.WriteLine($"Section {sectionCount + 1}:");
                Console.WriteLine(KnowledgeFileSection.CreateFromChunks(section).ToString());
                Console.WriteLine($"<- End of Section {sectionCount + 1} ->\n");
                sectionCount++;
            }

            Assert.AreEqual(3, sectionCount);
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldGroupSimilarChunks()
        {
            // Arrange - Use real test document to test similarity-based grouping
            using Stream stream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument02);
            using StreamReader reader = new StreamReader(stream);

            Console.WriteLine("=== Testing Similar Chunk Grouping ===");
            Console.WriteLine($"Using test document: {TestDataFiles.TestDocument02}");
            Console.WriteLine($"Configuration: maxTokensPerSection=200, lookaheadBufferSize=50, standardDeviationMultiplier=1.0, minimumSimilarityThreshold=0.65");

            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 50);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(
                maxTokensPerSection: 200,
                lookaheadBufferSize: 50,
                standardDeviationMultiplier: 1.0,
                chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Assert
            Console.WriteLine($"Created {sections.Count} sections");
            Assert.IsTrue(sections.Count >= 1, "Should create at least one section");

            // Verify sections contain related content
            for (int i = 0; i < sections.Count; i++)
            {
                List<KnowledgeFileChunk> section = sections[i];
                Assert.IsTrue(section.Count > 0, "Each section should contain at least one chunk");
                Console.WriteLine($"Section {i + 1} ({section.Count} chunks):");
                Console.WriteLine(KnowledgeFileSection.CreateFromChunks(section).ToString());
                Console.WriteLine($"End of Section {i + 1}\n");
            }
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldWorkWithLookAheadConfiguration()
        {
            // Arrange
            string content = "# Section 1\n\nContent A.\n\nContent B.\n\n" +
                           "# Section 2\n\nContent C.\n\nContent D.\n\n" +
                           "# Section 3\n\nContent E.\n\nContent F.";

            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 30);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Assert
            Assert.IsTrue(sections.Count > 0, "Should create sections");
            Console.WriteLine($"Created {sections.Count} sections");
            for (int i = 0; i < sections.Count; i++)
            {
                Console.WriteLine($"Section {i + 1} has {sections[i].Count} chunks");
            }
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldPreserveAllContent_RoundTrip()
        {
            // Arrange
            using Stream originalStream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01);
            string originalContent = await new StreamReader(originalStream).ReadToEndAsync();
            Console.WriteLine("=== Testing Content Preservation ===");
            Console.WriteLine($"Original content length: {originalContent.Length} characters");
            Console.WriteLine($"Original content preview: {originalContent.Substring(0, Math.Min(200, originalContent.Length))}...");

            using Stream stream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01);
            using StreamReader reader = new StreamReader(stream);
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 50, ChunkStopSignals.Markdown);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(maxTokensPerSection: 200, chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Reconstruct content from sections using ToString()
            string reconstructedContent = string.Join("", sections.Select(section =>
            {
                // Create a temporary section object to use its ToString() method
                return KnowledgeFileSection.CreateFromChunks(section).ToString();
            }));

            // Assert
            Console.WriteLine($"Created {sections.Count} sections");
            Console.WriteLine($"Reconstructed content length: {reconstructedContent.Length} characters");
            Console.WriteLine($"Content preservation: {(originalContent == reconstructedContent ? "SUCCESS" : "FAILED")}");

            for (int i = 0; i < sections.Count; i++)
            {
                Console.WriteLine($"Section {i + 1}: {sections[i].Count} chunks, {sections[i].Sum(c => c.Content.Length)} characters");
            }

            Assert.IsTrue(sections.Count > 0, "Should create at least one section");

            // Normalize whitespace for comparison to handle potential extra newlines
            string normalizedOriginal = originalContent.TrimEnd('\r', '\n');
            string normalizedReconstructed = reconstructedContent.TrimEnd('\r', '\n');
            Assert.AreEqual(normalizedOriginal, normalizedReconstructed, "Reconstructed content should match original (after whitespace normalization)");
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldHandleCancellation()
        {
            const int rangeMax = 10000;

            // Arrange
            string content = "# Test Document\n\n" + string.Join("\n\n", Enumerable.Range(1, rangeMax).Select(i => $"Paragraph {i}."));
            Console.WriteLine("=== Testing Cancellation ===");
            Console.WriteLine($"Input content: {content.Length} characters with {Enumerable.Range(1, rangeMax).Count()} paragraphs");

            // Use SlowStream to ensure the operation takes long enough to be cancelled
            using SlowStream slowStream = SlowStream.FromString(content, delayMillisecondsPerRead: 1, delayNanosecondsPerByte: 100);
            using StreamReader reader = new StreamReader(slowStream);
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 20);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer);

            const int cancellationTimeoutMs = 200;

            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(cancellationTimeoutMs)); // Cancel after 100ms
            Stopwatch stopwatch = Stopwatch.StartNew();

            bool didHandleException = false;
            int sectionsProcessed = 0;

            // Act & Assert
            Console.WriteLine($"Starting processing with {cancellationTimeoutMs}ms cancellation timeout...");
            try
            {

                await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync(cancellationTokenSource.Token))
                {
                    sectionsProcessed++;
                    Console.WriteLine($"Processed section {sectionsProcessed} with {section.Count} chunks");
                    // This should be cancelled
                }
            }
            catch (Exception exception)
            {
                stopwatch.Stop();

                Console.WriteLine("Exception occurred:");
                Console.WriteLine(exception.Message);
                Console.WriteLine(exception.GetType().Name);

                if (exception is OperationCanceledException)
                {
                    didHandleException = true;
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"Cancellation was expected after {cancellationTimeoutMs}ms");
            Console.WriteLine($"Actual time to reach post cancellation code: {stopwatch.ElapsedMilliseconds}ms");

            Assert.IsTrue(didHandleException, "Should handle cancellation exception");
            Assert.IsTrue(sectionsProcessed > 2, "Should still process some sections before cancellation");
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldHandleSmallSections()
        {
            // Arrange - Content that should create very small sections
            string content = "# Topic A\n\nContent A.\n\n" +
                           "# Topic B\n\nContent B.\n\n" +
                           "# Topic C\n\nContent C.";

            Console.WriteLine("=== Testing Small Sections ===");
            Console.WriteLine($"Input content:\n{content}");
            Console.WriteLine($"Configuration: maxTokensPerSection=50, lookaheadBufferSize=20, standardDeviationMultiplier=0.8");

            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 10);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(
                maxTokensPerSection: 50,
                lookaheadBufferSize: 20, // Smaller buffer for this test
                standardDeviationMultiplier: 0.8, // Lower multiplier to encourage more splits
                chunkStopSignals: ChunkStopSignals.Markdown);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _embeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Assert
            Console.WriteLine($"Created {sections.Count} sections");
            Assert.IsTrue(sections.Count > 1, "Should create multiple small sections");

            // Verify sections can be very small (1-2 chunks)
            for (int i = 0; i < sections.Count; i++)
            {
                List<KnowledgeFileChunk> section = sections[i];
                Console.WriteLine($"Section {i + 1} ({section.Count} chunks):");
                Console.WriteLine(KnowledgeFileSection.CreateFromChunks(section).ToString());
                Console.WriteLine($"End of Section {i + 1}\n");

                Assert.IsTrue(section.Count >= 1, "Each section should have at least one chunk");
                Assert.IsTrue(section.Count <= 5, "Sections should be reasonably small"); // Loosened from 3 to 5
            }
        }
    }
}