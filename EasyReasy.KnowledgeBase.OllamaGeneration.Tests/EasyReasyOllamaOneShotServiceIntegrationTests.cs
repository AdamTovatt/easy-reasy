using EasyReasy.EnvironmentVariables;
using System.Diagnostics;

namespace EasyReasy.KnowledgeBase.OllamaGeneration.Tests
{
    [TestClass]
    public sealed class EasyReasyOllamaOneShotServiceIntegrationTests
    {
        private static EasyReasyOllamaOneShotService? _oneShotService = null;

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
                string chatModel = OllamaTestEnvironmentVariables.OllamaChatModelName.GetValue();

                _oneShotService = await EasyReasyOllamaOneShotService.CreateAsync(baseUrl, apiKey, chatModel);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to create Ollama one-shot service: {exception.Message}");
                Assert.Inconclusive("Failed to initialize Ollama one-shot service for integration tests.");
            }
        }

        [ClassCleanup]
        public static void AfterAll()
        {
            _oneShotService?.Dispose();
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldGenerateTextResponse()
        {
            // Skip test if one-shot service is not available
            if (_oneShotService == null)
            {
                Assert.Inconclusive("One-shot service not available. Check environment variables and Ollama server.");
                return;
            }

            // Arrange
            string systemPrompt = "You are a helpful assistant. Please respond with exactly 'Hello World' and nothing else.";
            string userInput = "Say hello.";

            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();
            string response = await _oneShotService.ProcessAsync(systemPrompt, userInput);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(response, "Response should not be null");
            Assert.IsTrue(response.Length > 0, "Response should not be empty");

            Console.WriteLine($"Generated response in {stopwatch.ElapsedMilliseconds}ms:");
            Console.WriteLine($"System: {systemPrompt}");
            Console.WriteLine($"User: {userInput}");
            Console.WriteLine($"Assistant: {response}");
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldHandleSummarizationTask()
        {
            // Skip test if one-shot service is not available
            if (_oneShotService == null)
            {
                Assert.Inconclusive("One-shot service not available. Check environment variables and Ollama server.");
                return;
            }

            // Arrange
            string systemPrompt = "Summarize the following text in exactly one sentence:";
            string userInput = @"Machine learning is a subset of artificial intelligence that focuses on the development of algorithms and statistical models that enable computer systems to improve their performance on a specific task through experience without being explicitly programmed. It involves training algorithms on large datasets to recognize patterns and make predictions or decisions. Common applications include image recognition, natural language processing, recommendation systems, and autonomous vehicles. The field has grown rapidly due to increases in computational power, availability of big data, and advances in algorithmic techniques.";

            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();
            string response = await _oneShotService.ProcessAsync(systemPrompt, userInput);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(response, "Response should not be null");
            Assert.IsTrue(response.Length > 0, "Response should not be empty");
            Assert.IsTrue(response.Length < userInput.Length, "Summary should be shorter than original text");

            Console.WriteLine($"Summarization completed in {stopwatch.ElapsedMilliseconds}ms:");
            Console.WriteLine($"Original text: {userInput.Length} characters");
            Console.WriteLine($"Summary: {response} ({response.Length} characters)");
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldHandleQuestionGenerationTask()
        {
            // Skip test if one-shot service is not available
            if (_oneShotService == null)
            {
                Assert.Inconclusive("One-shot service not available. Check environment variables and Ollama server.");
                return;
            }

            // Arrange
            string systemPrompt = @"Generate exactly 3 questions based on the following text. Return only the questions, one per line, numbered 1., 2., 3.";
            string userInput = @"Photosynthesis is the process by which plants use sunlight, water, and carbon dioxide to produce oxygen and energy in the form of sugar. This process occurs mainly in the leaves of plants, specifically in tiny structures called chloroplasts that contain a green pigment called chlorophyll.";

            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();
            string response = await _oneShotService.ProcessAsync(systemPrompt, userInput);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(response, "Response should not be null");
            Assert.IsTrue(response.Length > 0, "Response should not be empty");
            Assert.IsTrue(response.Contains("?"), "Response should contain question marks");

            Console.WriteLine($"Question generation completed in {stopwatch.ElapsedMilliseconds}ms:");
            Console.WriteLine($"Input: {userInput}");
            Console.WriteLine($"Generated questions:");
            Console.WriteLine(response);
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldHandleCancellation()
        {
            // Skip test if one-shot service is not available
            if (_oneShotService == null)
            {
                Assert.Inconclusive("One-shot service not available. Check environment variables and Ollama server.");
                return;
            }

            // Arrange
            string systemPrompt = "Write a very long story about artificial intelligence.";
            string userInput = "Tell me a detailed story.";
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100)); // Short timeout

            // Act & Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await _oneShotService.ProcessAsync(systemPrompt, userInput, cancellationTokenSource.Token);
            });
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldThrowArgumentException_WhenSystemPromptIsEmpty()
        {
            // Skip test if one-shot service is not available
            if (_oneShotService == null)
            {
                Assert.Inconclusive("One-shot service not available. Check environment variables and Ollama server.");
                return;
            }

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await _oneShotService.ProcessAsync(string.Empty, "Valid input");
            });

            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await _oneShotService.ProcessAsync("   ", "Valid input");
            });
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldThrowArgumentException_WhenUserInputIsEmpty()
        {
            // Skip test if one-shot service is not available
            if (_oneShotService == null)
            {
                Assert.Inconclusive("One-shot service not available. Check environment variables and Ollama server.");
                return;
            }

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await _oneShotService.ProcessAsync("Valid system prompt", string.Empty);
            });

            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await _oneShotService.ProcessAsync("Valid system prompt", "   ");
            });
        }

        [TestMethod]
        public async Task ProcessAsync_ShouldHandleContextualizationTask()
        {
            // Skip test if one-shot service is not available
            if (_oneShotService == null)
            {
                Assert.Inconclusive("One-shot service not available. Check environment variables and Ollama server.");
                return;
            }

            // Arrange
            string systemPrompt = @"You are helping to contextualize a document chunk. Provide a brief context summary that explains what this text is about and how it might relate to a larger document about programming concepts.";
            string userInput = @"The finally block is executed regardless of whether an exception occurs or not. This makes it ideal for cleanup operations like closing files, releasing database connections, or disposing of resources.";

            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();
            string response = await _oneShotService.ProcessAsync(systemPrompt, userInput);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(response, "Response should not be null");
            Assert.IsTrue(response.Length > 0, "Response should not be empty");

            Console.WriteLine($"Contextualization completed in {stopwatch.ElapsedMilliseconds}ms:");
            Console.WriteLine($"Input chunk: {userInput}");
            Console.WriteLine($"Context: {response}");
        }
    }
}
