using EasyReasy.EnvironmentVariables;
using EasyReasy.KnowledgeBase.BertTokenization;
using EasyReasy.KnowledgeBase.Chunking;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Models;
using EasyReasy.KnowledgeBase.OllamaGeneration;
using EasyReasy.KnowledgeBase.Searching;
using EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore;
using EasyReasy.KnowledgeBase.Storage.Sqlite;
using EasyReasy.KnowledgeBase.Tests.TestUtilities;
using System.Reflection;

namespace EasyReasy.KnowledgeBase.Tests.Searching
{
    [TestClass]
    public class SearchableKnowledgeBaseTests
    {
        private const string _persistentEmbeddingPath = TestPaths.PersistentEmbeddingServicePath;

        private static ResourceManager _resourceManager = null!;
        private static ITokenizer _tokenizer = null!;
        private static IEmbeddingService? _ollamaEmbeddingService = null;
        private static PersistentEmbeddingService? _persistentEmbeddingService = null;

        [ClassInitialize]
        public static async Task BeforeAll(TestContext testContext)
        {
            _resourceManager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly());
            _tokenizer = await BertTokenizer.CreateAsync();

            // Load environment variables from test configuration file
            try
            {
                EnvironmentVariableHelper.LoadVariablesFromFile(TestPaths.TestEnvironmentVariables);
                EnvironmentVariableHelper.ValidateVariableNamesIn(typeof(OllamaTestEnvironmentVariables));

                _ollamaEmbeddingService = await EasyReasyOllamaEmbeddingService.CreateAsync(
                    OllamaTestEnvironmentVariables.OllamaBaseUrl.GetValue(),
                    OllamaTestEnvironmentVariables.OllamaApiKey.GetValue(),
                    OllamaTestEnvironmentVariables.OllamaEmbeddingModelName.GetValue());

                // Initialize persistent embedding service
                _persistentEmbeddingService = await InitializePersistentEmbeddingServiceAsync(
                    TestDataFiles.TestDocument01,
                    TestDataFiles.TestDocument02,
                    TestDataFiles.TestDocument03,
                    TestDataFiles.TestDocument04);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Could not load TestEnvironmentVariables.txt: {exception.Message}");
                Assert.Inconclusive();
            }
        }

        [ClassCleanup]
        public static void AfterAll()
        {
            if (_ollamaEmbeddingService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private static SectionReader CreateSectionReader(Stream contentStream, Guid fileId, IEmbeddingService embeddingService)
        {
            SectionReaderFactory factory = new SectionReaderFactory(embeddingService, _tokenizer);
            SectionReader sectionReader = factory.CreateForMarkdown(contentStream, fileId, maxTokensPerChunk: 100, maxTokensPerSection: 1000);
            return sectionReader;
        }

        /// <summary>
        /// Initializes a persistent embedding service by either loading from an existing file
        /// or creating embeddings for the specified documents and saving them.
        /// </summary>
        /// <param name="documents">The test documents to create embeddings for.</param>
        /// <returns>A persistent embedding service with embeddings for all document chunks.</returns>
        private static async Task<PersistentEmbeddingService> InitializePersistentEmbeddingServiceAsync(params Resource[] documents)
        {
            if (_ollamaEmbeddingService == null)
            {
                throw new NullReferenceException("The ollama embedding service was null");
            }

            // Check if persistent embedding service file already exists
            if (File.Exists(_persistentEmbeddingPath))
            {
                Console.WriteLine($"Loading existing persistent embedding service from {_persistentEmbeddingPath}");
                return PersistentEmbeddingService.Deserialize(_persistentEmbeddingPath);
            }

            Console.WriteLine($"Creating new persistent embedding service for {documents.Length} documents");

            // Create a new persistent embedding service
            PersistentEmbeddingService persistentService = new PersistentEmbeddingService(_ollamaEmbeddingService.ModelName, _ollamaEmbeddingService.Dimensions);

            // Process each document
            foreach (Resource document in documents)
            {
                Console.WriteLine($"Processing document: {document.Path}");

                Guid fileId = Guid.NewGuid();
                using Stream stream = await _resourceManager.GetResourceStreamAsync(document);

                // Create section reader with real embedding service
                SectionReader sectionReader = CreateSectionReader(stream, fileId, _ollamaEmbeddingService);

                // Read sections and extract embeddings from chunks
                await foreach (List<KnowledgeFileChunk> chunks in sectionReader.ReadSectionsAsync())
                {
                    foreach (KnowledgeFileChunk chunk in chunks)
                    {
                        if (chunk.ContainsVector())
                        {
                            // Get the text content for this chunk
                            string chunkText = chunk.Content;

                            // Get the embedding from the chunk
                            float[]? embedding = chunk.Embedding;

                            if (embedding == null)
                            {
                                throw new NullReferenceException($"The chunk embedding for a chunk was unexpectedly null");
                            }

                            // Add to persistent service
                            persistentService.AddEmbedding(chunkText, embedding);
                        }
                    }
                }
            }

            // Save the persistent embedding service
            Console.WriteLine($"Saving persistent embedding service to {_persistentEmbeddingPath}");
            persistentService.Serialize(_persistentEmbeddingPath);

            return persistentService;
        }

        [TestMethod]
        public async Task SearchAsync_WithTestDocuments_ShouldReturnRelevantResults()
        {
            // Skip test if persistent embedding service is not available
            if (_persistentEmbeddingService == null)
            {
                Assert.Inconclusive("Persistent embedding service not available. Set environment variables to run integration tests.");
                return;
            }

            if (_ollamaEmbeddingService == null)
            {
                Assert.Inconclusive("Ollama embedding service not available. Set environment variables to run integration tests.");
                return;
            }

            // Arrange
            Dictionary<Guid, Resource> filesToIndex = new Dictionary<Guid, Resource>();
            filesToIndex.Add(Guid.NewGuid(), TestDataFiles.TestDocument03);

            SqliteKnowledgeStore sqliteKnowledgeStore = await SqliteKnowledgeStore.CreateAsync(TestPaths.SqliteKnowledgeStorePath);
            EasyReasyVectorStore chunksVectorStore = new EasyReasyVectorStore(new VectorStorage.CosineVectorStore(_persistentEmbeddingService.Dimensions));
            SearchableKnowledgeStore searchableKnowledgeStore = new SearchableKnowledgeStore(sqliteKnowledgeStore, chunksVectorStore);
            SearchableKnowledgeBase searchableKnowledgeBase = new SearchableKnowledgeBase(searchableKnowledgeStore, _ollamaEmbeddingService);

            foreach (Guid fileId in filesToIndex.Keys)
            {
                using (Stream resourceStream = await _resourceManager.GetResourceStreamAsync(filesToIndex[fileId]))
                {
                    SectionReader sectionReader = CreateSectionReader(resourceStream, fileId, _persistentEmbeddingService);

                    await sqliteKnowledgeStore.Files.AddAsync(new KnowledgeFile(fileId, filesToIndex[fileId].Path, new byte[1])); // TODO! use real hash

                    int sectionIndex = 0;
                    await foreach (List<KnowledgeFileChunk> chunks in sectionReader.ReadSectionsAsync())
                    {
                        KnowledgeFileSection section = KnowledgeFileSection.CreateFromChunks(chunks, fileId, sectionIndex);
                        await sqliteKnowledgeStore.Sections.AddAsync(section);

                        foreach (KnowledgeFileChunk chunk in chunks)
                        {
                            await sqliteKnowledgeStore.Chunks.AddAsync(chunk);

                            if (chunk.Embedding == null)
                            {
                                throw new ArgumentNullException(nameof(chunk));
                            }

                            await chunksVectorStore.AddAsync(chunk.Id, chunk.Embedding);
                        }

                        sectionIndex++;
                    }
                }
            }

            // Act
            IKnowledgeBaseSearchResult searchResult = await searchableKnowledgeBase.SearchAsync("How does authentication work?", 3);
            KnowledgeBaseSearchResult? result = searchResult as KnowledgeBaseSearchResult;

            // Assert
            Assert.IsNotNull(result);

            Console.WriteLine(result.GetAsContextString());
        }
    }
}
