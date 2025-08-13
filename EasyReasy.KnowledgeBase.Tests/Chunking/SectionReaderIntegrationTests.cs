using Microsoft.VisualStudio.TestTools.UnitTesting;
using EasyReasy.KnowledgeBase.BertTokenization;
using EasyReasy.KnowledgeBase.Chunking;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Tests.TestUtilities;
using EasyReasy.KnowledgeBase.Models;
using System.Reflection;
using System.Diagnostics;
using EasyReasy.EnvironmentVariables;

namespace EasyReasy.KnowledgeBase.Tests.Chunking
{
    [TestClass]
    public class SectionReaderIntegrationTests
    {
        private static ResourceManager _resourceManager = null!;
        private static ITokenizer _tokenizer = null!;
        private static IEmbeddingService? _ollamaEmbeddingService = null;

        [ClassInitialize]
        public static async Task BeforeAll(TestContext testContext)
        {
            _resourceManager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly());
            _tokenizer = await BertTokenizer.CreateAsync();

            // Load environment variables from test configuration file
            try
            {
                EnvironmentVariableHelper.LoadVariablesFromFile("..\\..\\TestEnvironmentVariables.txt");
                EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(OllamaTestEnvironmentVariables));
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Could not load TestEnvironmentVariables.txt: {exception.Message}");
                Assert.Inconclusive();
            }

            _ollamaEmbeddingService = new EasyReasyOllamaEmbeddingService(
                OllamaTestEnvironmentVariables.OllamaBaseUrl.GetValue(),
                OllamaTestEnvironmentVariables.OllamaApiKey.GetValue(),
                OllamaTestEnvironmentVariables.OllamaModelName.GetValue());
        }

        [ClassCleanup]
        public static void AfterAll()
        {
            if (_ollamaEmbeddingService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        [TestMethod]
        public async Task ReadSectionsAsync_WithRealEmbeddings_ShouldGroupSimilarContent()
        {
            // Skip test if Ollama service is not available
            if (_ollamaEmbeddingService == null)
            {
                Assert.Inconclusive("Ollama embedding service not available. Set environment variables to run integration tests.");
                return;
            }

            // Arrange - Use real test document to test similarity-based grouping with real embeddings
            using Stream stream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument02);
            using StreamReader reader = new StreamReader(stream);

            Console.WriteLine("=== Integration Test: Real Embeddings Similarity Grouping ===");
            Console.WriteLine($"Using test document: {TestDataFiles.TestDocument02}");

            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 50);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(
                maxTokensPerSection: 200,
                startThreshold: 0.8,
                stopThreshold: 0.7,
                confirmWindow: 1);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _ollamaEmbeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Assert
            Console.WriteLine($"Created {sections.Count} sections with real embeddings");
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
        public async Task ReadSectionsAsync_WithRealEmbeddings_ShouldPreserveAllContent()
        {
            // Skip test if Ollama service is not available
            if (_ollamaEmbeddingService == null)
            {
                Assert.Inconclusive("Ollama embedding service not available. Set environment variables to run integration tests.");
                return;
            }

            // Arrange
            using Stream originalStream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01);
            string originalContent = await new StreamReader(originalStream).ReadToEndAsync();
            Console.WriteLine("=== Integration Test: Content Preservation with Real Embeddings ===");
            Console.WriteLine($"Original content length: {originalContent.Length} characters");

            using Stream stream = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01);
            using StreamReader reader = new StreamReader(stream);
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 50);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(maxTokensPerSection: 200);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _ollamaEmbeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Reconstruct content from sections
            string reconstructedContent = string.Join("\n", sections.Select(section =>
            {
                return KnowledgeFileSection.CreateFromChunks(section).ToString();
            }));

            // Assert
            Console.WriteLine($"Created {sections.Count} sections with real embeddings");
            Console.WriteLine($"Reconstructed content length: {reconstructedContent.Length} characters");

            for (int i = 0; i < sections.Count; i++)
            {
                Console.WriteLine($"Section {i + 1}: {sections[i].Count} chunks, {sections[i].Sum(c => c.Content.Length)} characters");
            }

            Assert.IsTrue(sections.Count > 0, "Should create at least one section");

            // Normalize whitespace for comparison
            string normalizedOriginal = originalContent.TrimEnd('\r', '\n');
            string normalizedReconstructed = reconstructedContent.TrimEnd('\r', '\n');
            Assert.AreEqual(normalizedOriginal, normalizedReconstructed, "Reconstructed content should match original (after whitespace normalization)");
        }

        [TestMethod]
        public async Task ReadSectionsAsync_WithRealEmbeddings_ShouldHandleLargeDocument()
        {
            // Skip test if Ollama service is not available
            if (_ollamaEmbeddingService == null)
            {
                Assert.Inconclusive("Ollama embedding service not available. Set environment variables to run integration tests.");
                return;
            }

            // Arrange - Create a larger document by combining both test documents
            using Stream stream1 = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument01);
            using Stream stream2 = await _resourceManager.GetResourceStreamAsync(TestDataFiles.TestDocument02);
            string content1 = await new StreamReader(stream1).ReadToEndAsync();
            string content2 = await new StreamReader(stream2).ReadToEndAsync();
            string largeContent = content1 + "\n\n" + content2;

            Console.WriteLine("=== Integration Test: Large Document Processing ===");
            Console.WriteLine($"Large document length: {largeContent.Length} characters");

            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(largeContent)));
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 30);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration(
                maxTokensPerSection: 150,
                startThreshold: 0.75,
                stopThreshold: 0.65,
                confirmWindow: 2);
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _ollamaEmbeddingService, sectioningConfig, _tokenizer);

            // Act
            List<List<KnowledgeFileChunk>> sections = new List<List<KnowledgeFileChunk>>();
            await foreach (List<KnowledgeFileChunk> section in sectionReader.ReadSectionsAsync())
            {
                sections.Add(section);
            }

            // Assert
            Console.WriteLine($"Created {sections.Count} sections for large document");
            Assert.IsTrue(sections.Count > 1, "Should create multiple sections for large document");

            // Verify each section is within token limits
            foreach (List<KnowledgeFileChunk> section in sections)
            {
                int totalTokens = section.Sum(chunk => _tokenizer.CountTokens(chunk.Content));
                Assert.IsTrue(totalTokens <= sectioningConfig.MaxTokensPerSection,
                    $"Section should not exceed {sectioningConfig.MaxTokensPerSection} tokens, but has {totalTokens}");
            }

            // Verify all content is preserved
            string reconstructedContent = string.Join("\n", sections.Select(section =>
            {
                return KnowledgeFileSection.CreateFromChunks(section).ToString();
            }));

            string normalizedOriginal = largeContent.TrimEnd('\r', '\n');
            string normalizedReconstructed = reconstructedContent.TrimEnd('\r', '\n');
            Assert.AreEqual(normalizedOriginal, normalizedReconstructed, "All content should be preserved");
        }

        [TestMethod]
        public async Task ReadSectionsAsync_WithRealEmbeddings_ShouldHandleCancellation()
        {
            // Skip test if Ollama service is not available
            if (_ollamaEmbeddingService == null)
            {
                Assert.Inconclusive("Ollama embedding service not available. Set environment variables to run integration tests.");
                return;
            }

            const int rangeMax = 100;

            // Arrange
            string content = "# Test Document\n\n" + string.Join("\n\n", Enumerable.Range(1, rangeMax).Select(i => $"Paragraph {i}."));
            Console.WriteLine("=== Integration Test: Cancellation with Real Embeddings ===");
            Console.WriteLine($"Input content: {content.Length} characters with {rangeMax} paragraphs");

            using StreamReader reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
            ChunkingConfiguration chunkingConfig = new ChunkingConfiguration(_tokenizer, 20);
            SectioningConfiguration sectioningConfig = new SectioningConfiguration();
            TextSegmentReader textSegmentReader = TextSegmentReader.CreateForMarkdown(reader);
            SegmentBasedChunkReader chunkReader = new SegmentBasedChunkReader(textSegmentReader, chunkingConfig);
            SectionReader sectionReader = new SectionReader(chunkReader, _ollamaEmbeddingService, sectioningConfig, _tokenizer);

            const int cancellationTimeoutMs = 2000; // 2 seconds for real embeddings

            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(cancellationTimeoutMs));
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
            Assert.IsTrue(sectionsProcessed > 0, "Should process at least some sections before cancellation");
        }
    }
}