using EasyReasy.EnvironmentVariables;

namespace EasyReasy.KnowledgeBase.OllamaGeneration.Tests
{
    [TestClass]
    public sealed class EasyReasyOllamaEmbeddingServiceIntegrationTests
    {
        private static EasyReasyOllamaEmbeddingService? _embeddingService = null;

        [ClassInitialize]
        public static async Task BeforeAll(TestContext testContext)
        {
            // Load environment variables from test configuration file
            try
            {
                EnvironmentVariableHelper.LoadVariablesFromFile("..\\..\\TestEnvironmentVariables.txt");
                EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(OllamaTestEnvironmentVariables));
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Could not load TestEnvironmentVariables.txt: {exception.Message}");
                Assert.Inconclusive("Integration tests require TestEnvironmentVariables.txt configuration file.");
            }

            try
            {
                string baseUrl = OllamaTestEnvironmentVariables.OllamaBaseUrl.GetValue();
                string apiKey = OllamaTestEnvironmentVariables.OllamaApiKey.GetValue();
                string embeddingModel = OllamaTestEnvironmentVariables.OllamaEmbeddingModelName.GetValue();

                _embeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(baseUrl, apiKey, embeddingModel);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to create Ollama embedding service: {exception.Message}");
                Assert.Inconclusive("Failed to initialize Ollama embedding service for integration tests.");
            }
        }

        [ClassCleanup]
        public static void AfterAll()
        {
            _embeddingService?.Dispose();
        }

        [TestMethod]
        public async Task EmbedAsync_ShouldGenerateEmbeddingVector()
        {
            // Skip test if embedding service is not available
            if (_embeddingService == null)
            {
                Assert.Inconclusive("Embedding service not available. Check environment variables and Ollama server.");
                return;
            }

            // Arrange
            string testText = "This is a test sentence for embedding generation.";

            // Act
            float[] embeddings = await _embeddingService.EmbedAsync(testText);

            // Assert
            Assert.IsNotNull(embeddings, "Embeddings should not be null");
            Assert.IsTrue(embeddings.Length > 0, "Embeddings should have at least one dimension");
            Assert.IsTrue(embeddings.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), "All embedding values should be valid numbers");

            Console.WriteLine($"Generated embedding vector with {embeddings.Length} dimensions");
            Console.WriteLine($"Sample values: [{string.Join(", ", embeddings.Take(5).Select(f => f.ToString("F6")))}...]");
        }

        [TestMethod]
        public async Task EmbedAsync_ShouldHandleCancellation()
        {
            // Skip test if embedding service is not available
            if (_embeddingService == null)
            {
                Assert.Inconclusive("Embedding service not available. Check environment variables and Ollama server.");
                return;
            }

            // Arrange
            string testText = "This is a test sentence that should be cancelled during processing.";
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(1)); // Very short timeout

            // Act & Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await _embeddingService.EmbedAsync(testText, cancellationTokenSource.Token);
            });
        }

        [TestMethod]
        public async Task EmbedAsync_ShouldThrowArgumentException_WhenTextIsEmpty()
        {
            // Skip test if embedding service is not available
            if (_embeddingService == null)
            {
                Assert.Inconclusive("Embedding service not available. Check environment variables and Ollama server.");
                return;
            }

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await _embeddingService.EmbedAsync(string.Empty);
            });

            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await _embeddingService.EmbedAsync("   ");
            });
        }

        [TestMethod]
        public async Task EmbedAsync_ShouldProduceSimilarEmbeddingsForSimilarText()
        {
            // Skip test if embedding service is not available
            if (_embeddingService == null)
            {
                Assert.Inconclusive("Embedding service not available. Check environment variables and Ollama server.");
                return;
            }

            // Arrange
            string text1 = "The cat is sleeping on the couch.";
            string text2 = "A cat is resting on the sofa.";
            string text3 = "The weather is sunny today.";

            // Act
            float[] embedding1 = await _embeddingService.EmbedAsync(text1);
            float[] embedding2 = await _embeddingService.EmbedAsync(text2);
            float[] embedding3 = await _embeddingService.EmbedAsync(text3);

            // Assert - similar texts should have higher similarity than dissimilar ones
            double similarity12 = CosineSimilarity(embedding1, embedding2);
            double similarity13 = CosineSimilarity(embedding1, embedding3);

            Assert.IsTrue(similarity12 > similarity13,
                $"Similar texts should have higher similarity. Similarity 1-2: {similarity12:F4}, Similarity 1-3: {similarity13:F4}");

            Console.WriteLine($"Text 1: {text1}");
            Console.WriteLine($"Text 2: {text2}");
            Console.WriteLine($"Text 3: {text3}");
            Console.WriteLine($"Similarity 1-2 (similar): {similarity12:F4}");
            Console.WriteLine($"Similarity 1-3 (different): {similarity13:F4}");
        }

        private static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                throw new ArgumentException("Vectors must have the same length");

            double dotProduct = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}
