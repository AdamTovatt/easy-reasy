using EasyReasy.KnowledgeBase.BertTokenization;
using EasyReasy.KnowledgeBase.Chunking;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Models;
using EasyReasy.KnowledgeBase.Tests.TestUtilities;
using System.Diagnostics;
using System.Reflection;

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

            Guid fileId = Guid.NewGuid();
            using Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            SectionReaderFactory factory = new SectionReaderFactory(_embeddingService, _tokenizer);
            SectionReader sectionReader = factory.CreateForMarkdown(stream, fileId, maxTokensPerChunk: 100, maxTokensPerSection: 200);

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

            Guid fileId = Guid.NewGuid();
            using Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            SectionReaderFactory factory = new SectionReaderFactory(_embeddingService, _tokenizer);
            SectionReader sectionReader = factory.CreateForMarkdown(stream, fileId, maxTokensPerChunk: 100, maxTokensPerSection: 200);

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
                Console.WriteLine(KnowledgeFileSection.CreateFromChunks(sections[i], fileId, i).ToString());
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

            Guid fileId = Guid.NewGuid();
            using Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            SectionReaderFactory factory = new SectionReaderFactory(_embeddingService, _tokenizer);
            SectionReader sectionReader = factory.CreateForMarkdown(stream, fileId, maxTokensPerChunk: 100, maxTokensPerSection: 120);

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
                Console.WriteLine(KnowledgeFileSection.CreateFromChunks(section, fileId, sectionCount).ToString());
                Console.WriteLine($"<- End of Section {sectionCount + 1} ->\n");
                sectionCount++;
            }

            Assert.AreEqual(3, sectionCount);
        }

        [TestMethod]
        public async Task ReadSectionsAsync_ShouldGroupSimilarChunks()
        {
            // Arrange - Use real test document to test similarity-based grouping
            Guid fileId = Guid.NewGuid();
            using Stream stream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument02);

            Console.WriteLine("=== Testing Similar Chunk Grouping ===");
            Console.WriteLine($"Using test document: {TestDataFiles.TestDocument02}");
            Console.WriteLine($"Configuration: maxTokensPerSection=200, lookaheadBufferSize=50, standardDeviationMultiplier=1.0, minimumSimilarityThreshold=0.65");

            SectionReaderFactory factory = new SectionReaderFactory(_embeddingService, _tokenizer);
            SectionReader sectionReader = factory.CreateForMarkdown(stream, fileId, maxTokensPerChunk: 50, maxTokensPerSection: 200);

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
                Console.WriteLine(KnowledgeFileSection.CreateFromChunks(section, fileId, i).ToString());
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

            Guid fileId = Guid.NewGuid();
            using Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            SectionReaderFactory factory = new SectionReaderFactory(_embeddingService, _tokenizer);
            SectionReader sectionReader = factory.CreateForMarkdown(stream, fileId, maxTokensPerChunk: 30, maxTokensPerSection: 200);

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

            Guid fileId = Guid.NewGuid();
            using Stream stream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01);
            SectionReaderFactory factory = new SectionReaderFactory(_embeddingService, _tokenizer);
            SectionReader sectionReader = factory.CreateForMarkdown(stream, fileId, maxTokensPerChunk: 50, maxTokensPerSection: 200);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Reconstruct content from sections using ToString()
            string reconstructedContent = string.Join("", sections.Select((section, index) =>
            {
                // Create a temporary section object to use its ToString() method
                return KnowledgeFileSection.CreateFromChunks(section, fileId, index).ToString();
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

            Guid fileId = Guid.NewGuid();
            // Use SlowStream to ensure the operation takes long enough to be cancelled
            using (SlowStream slowStream = SlowStream.FromString(content, delayMillisecondsPerRead: 1, delayNanosecondsPerByte: 100))
            {
                SectionReaderFactory factory = new SectionReaderFactory(_embeddingService, _tokenizer);
                using SectionReader sectionReader = factory.CreateForMarkdown(slowStream, fileId, maxTokensPerChunk: 20, maxTokensPerSection: 200);

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
        }
    }
}