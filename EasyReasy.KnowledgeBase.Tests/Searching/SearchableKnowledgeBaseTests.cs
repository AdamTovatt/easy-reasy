using EasyReasy.EnvironmentVariables;
using EasyReasy.KnowledgeBase.BertTokenization;
using EasyReasy.KnowledgeBase.Chunking;
using EasyReasy.KnowledgeBase.ConfidenceRating;
using EasyReasy.KnowledgeBase.Generation;
using EasyReasy.KnowledgeBase.Models;
using EasyReasy.KnowledgeBase.OllamaGeneration;
using EasyReasy.KnowledgeBase.Indexing;
using EasyReasy.KnowledgeBase.Searching;
using EasyReasy.KnowledgeBase.Storage.IntegratedVectorStore;
using EasyReasy.KnowledgeBase.Storage.Sqlite;
using EasyReasy.KnowledgeBase.Tests.TestFileSources;
using EasyReasy.KnowledgeBase.Tests.TestUtilities;
using System.Diagnostics;
using System.Reflection;
using EasyReasy.VectorStorage;

namespace EasyReasy.KnowledgeBase.Tests.Searching
{
    [TestClass]
    public class SearchableKnowledgeBaseTests
    {
        private const string _persistentEmbeddingPath = TestPaths.PersistentEmbeddingServicePath;

        private static ResourceManager _resourceManager = null!;
        private static ITokenizer _tokenizer = null!;
        private static IEmbeddingService _ollamaEmbeddingService = null!;
        private static PersistentEmbeddingService _persistentEmbeddingService = null!;
        private static SearchableKnowledgeBase _searchableKnowledgeBase = null!;
        private static CosineVectorStore _cosineVectorStore = null!;

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
                _persistentEmbeddingService = await PersistentEmbeddingService.InitializeFromDocumentsAsync(
                    _persistentEmbeddingPath,
                    _ollamaEmbeddingService,
                    _tokenizer,
                    _resourceManager,
                    maxTokensPerChunk: 100,
                    maxTokensPerSection: 1000,
                    TestDataFiles.TestDocument01,
                    TestDataFiles.TestDocument02,
                    TestDataFiles.TestDocument03,
                    TestDataFiles.TestDocument04);

                _searchableKnowledgeBase = await CreateSearchableKnowledgeBaseAsync();

                await IndexFileSourcesAsync(CreateFileSourceProvider());
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Could not load TestEnvironmentVariables.txt: {exception.Message}");
                Assert.Inconclusive();
            }
        }

        private static async Task<SearchableKnowledgeBase> CreateSearchableKnowledgeBaseAsync()
        {
            SqliteKnowledgeStore sqliteKnowledgeStore = await SqliteKnowledgeStore.CreateAsync(TestPaths.SqliteKnowledgeStorePath);

            _cosineVectorStore = new CosineVectorStore(_persistentEmbeddingService.Dimensions);

            if (File.Exists(TestPaths.CosineVectorStorePath))
            {
                using (Stream vectorStoreStream = File.OpenRead(TestPaths.CosineVectorStorePath))
                {
                    await _cosineVectorStore.LoadAsync(vectorStoreStream);
                }
            }

            EasyReasyVectorStore chunksVectorStore = new EasyReasyVectorStore(_cosineVectorStore);

            SearchableKnowledgeStore searchableKnowledgeStore = new SearchableKnowledgeStore(sqliteKnowledgeStore, chunksVectorStore);
            SearchableKnowledgeBase searchableKnowledgeBase = new SearchableKnowledgeBase(searchableKnowledgeStore, _ollamaEmbeddingService, _tokenizer);

            return searchableKnowledgeBase;
        }

        private static async Task IndexFileSourcesAsync(IFileSourceProvider fileSourceProvider)
        {
            IIndexer indexer = _searchableKnowledgeBase.CreateIndexer(_persistentEmbeddingService);

            foreach (IFileSource fileSource in await fileSourceProvider.GetAllFilesAsync())
            {
                await indexer.ConsumeAsync(fileSource);
            }

            await _cosineVectorStore.SaveAsync(File.OpenWrite(TestPaths.CosineVectorStorePath));
        }

        private static IFileSourceProvider CreateFileSourceProvider()
        {
            InMemoryFileSourceProvider inMemoryFileSourceProvider = new InMemoryFileSourceProvider();

            inMemoryFileSourceProvider.AddFile(new TestFileSource(Guid.Parse("771d384b-0956-4bb0-90c8-39c2db4e8461"), TestDataFiles.TestDocument03, _resourceManager));

            return inMemoryFileSourceProvider;
        }

        [ClassCleanup]
        public static void AfterAll()
        {
            if (_ollamaEmbeddingService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        [DataTestMethod]
        [DataRow("How does authentication work?")]
        public async Task SearchAsync_WithTestDocuments_ShouldReturnRelevantResults(string query)
        {
            // Act
            Stopwatch stopwatch = Stopwatch.StartNew();
            IKnowledgeBaseSearchResult searchResult = await _searchableKnowledgeBase.SearchAsync(query, 5);
            KnowledgeBaseSearchResult? result = searchResult as KnowledgeBaseSearchResult;

            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(result);

            foreach (RelevanceRatedEntry<KnowledgeFileSection> section in result.RelevantSections)
            {
                Console.WriteLine($"Relevance: {section.Relevance.RelevanceScore}");
            }

            Console.WriteLine($"Search time: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine(result.GetAsContextString());
        }
    }
}
