using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite.Tests
{
    [TestClass]
    public sealed class SqliteChunkStoreTests
    {
        private string _testDbPath = string.Empty;
        private SqliteChunkStore _chunkStore = null!;
        private SqliteFileStore _fileStore = null!;
        private SqliteSectionStore _sectionStore = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Use a temporary file for testing
            _testDbPath = Path.GetTempFileName();
            _fileStore = new SqliteFileStore($"Data Source={_testDbPath}");
            _sectionStore = new SqliteSectionStore($"Data Source={_testDbPath}");
            _chunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _chunkStore = null!;
            _sectionStore = null!;
            _fileStore = null!;

            // Try to delete the file with retries
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (File.Exists(_testDbPath))
                    {
                        File.Delete(_testDbPath);
                    }
                    break;
                }
                catch (IOException)
                {
                    if (i == 2) // Last attempt
                        break;

                    Thread.Sleep(100); // Wait a bit before retrying
                }
            }
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenConnectionStringIsNull()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new SqliteChunkStore(null!));
        }

        [TestMethod]
        public void Constructor_ShouldAccept_WhenConnectionStringIsValid()
        {
            // Act & Assert
            SqliteChunkStore store = new SqliteChunkStore("Data Source=:memory:");
            Assert.IsNotNull(store);
        }

        [TestMethod]
        public async Task AddAsync_ShouldAddChunk_WhenValidChunkProvided()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content",
                new float[] { 0.1f, 0.2f, 0.3f }
            );

            // Act
            await _chunkStore.AddAsync(chunk);

            // Assert
            KnowledgeFileChunk? retrieved = await _chunkStore.GetAsync(chunk.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(chunk.Id, retrieved.Id);
            Assert.AreEqual(chunk.SectionId, retrieved.SectionId);
            Assert.AreEqual(chunk.ChunkIndex, retrieved.ChunkIndex);
            Assert.AreEqual(chunk.Content, retrieved.Content);
            CollectionAssert.AreEqual(chunk.Embedding, retrieved.Embedding);
        }

        [TestMethod]
        public async Task AddAsync_ShouldThrow_WhenChunkIsNull()
        {
            // Arrange
            await _chunkStore.LoadAsync();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _chunkStore.AddAsync(null!));
        }

        [TestMethod]
        public async Task AddAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content"
            );

            // Act
            await _chunkStore.AddAsync(chunk);

            // Assert
            KnowledgeFileChunk? retrieved = await _chunkStore.GetAsync(chunk.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(chunk.Id, retrieved.Id);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnChunk_WhenChunkExists()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            await _chunkStore.AddAsync(chunk);

            // Act
            KnowledgeFileChunk? result = await _chunkStore.GetAsync(chunk.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(chunk.Id, result.Id);
            Assert.AreEqual(chunk.SectionId, result.SectionId);
            Assert.AreEqual(chunk.ChunkIndex, result.ChunkIndex);
            Assert.AreEqual(chunk.Content, result.Content);
            CollectionAssert.AreEqual(chunk.Embedding, result.Embedding);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnNull_WhenChunkDoesNotExist()
        {
            // Arrange
            await _chunkStore.LoadAsync();
            Guid nonExistentId = Guid.NewGuid();

            // Act
            KnowledgeFileChunk? result = await _chunkStore.GetAsync(nonExistentId);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetAsync_WithMultipleIds_ShouldReturnAllExistingChunks()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk1 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Chunk 1 content",
                new float[] { 0.1f, 0.2f }
            );
            KnowledgeFileChunk chunk2 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                1,
                "Chunk 2 content",
                new float[] { 0.3f, 0.4f }
            );
            KnowledgeFileChunk chunk3 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                2,
                "Chunk 3 content",
                new float[] { 0.5f, 0.6f }
            );

            await _chunkStore.AddAsync(chunk1);
            await _chunkStore.AddAsync(chunk2);
            await _chunkStore.AddAsync(chunk3);

            List<Guid> chunkIds = new List<Guid> { chunk1.Id, chunk2.Id, chunk3.Id };

            // Act
            IEnumerable<KnowledgeFileChunk> results = await _chunkStore.GetAsync(chunkIds);

            // Assert
            List<KnowledgeFileChunk> resultList = results.ToList();
            Assert.AreEqual(3, resultList.Count);
            Assert.IsTrue(resultList.Any(c => c.Id == chunk1.Id && c.Content == "Chunk 1 content"));
            Assert.IsTrue(resultList.Any(c => c.Id == chunk2.Id && c.Content == "Chunk 2 content"));
            Assert.IsTrue(resultList.Any(c => c.Id == chunk3.Id && c.Content == "Chunk 3 content"));
        }

        [TestMethod]
        public async Task GetAsync_WithMultipleIds_ShouldReturnOnlyExistingChunks()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk1 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Chunk 1 content"
            );
            KnowledgeFileChunk chunk2 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                1,
                "Chunk 2 content"
            );

            await _chunkStore.AddAsync(chunk1);
            await _chunkStore.AddAsync(chunk2);

            Guid nonExistentId = Guid.NewGuid();
            List<Guid> chunkIds = new List<Guid> { chunk1.Id, nonExistentId, chunk2.Id };

            // Act
            IEnumerable<KnowledgeFileChunk> results = await _chunkStore.GetAsync(chunkIds);

            // Assert
            List<KnowledgeFileChunk> resultList = results.ToList();
            Assert.AreEqual(2, resultList.Count);
            Assert.IsTrue(resultList.Any(c => c.Id == chunk1.Id));
            Assert.IsTrue(resultList.Any(c => c.Id == chunk2.Id));
            Assert.IsFalse(resultList.Any(c => c.Id == nonExistentId));
        }

        [TestMethod]
        public async Task GetAsync_WithMultipleIds_ShouldReturnEmptyCollection_WhenNoChunksExist()
        {
            // Arrange
            await _chunkStore.LoadAsync();
            List<Guid> nonExistentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            // Act
            IEnumerable<KnowledgeFileChunk> results = await _chunkStore.GetAsync(nonExistentIds);

            // Assert
            Assert.IsFalse(results.Any());
        }

        [TestMethod]
        public async Task GetAsync_WithMultipleIds_ShouldReturnEmptyCollection_WhenEmptyCollectionProvided()
        {
            // Arrange
            await _chunkStore.LoadAsync();
            List<Guid> emptyList = new List<Guid>();

            // Act
            IEnumerable<KnowledgeFileChunk> results = await _chunkStore.GetAsync(emptyList);

            // Assert
            Assert.IsFalse(results.Any());
        }

        [TestMethod]
        public async Task GetAsync_WithMultipleIds_ShouldThrow_WhenNullCollectionProvided()
        {
            // Arrange
            await _chunkStore.LoadAsync();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _chunkStore.GetAsync((IEnumerable<Guid>)null!));
        }

        [TestMethod]
        public async Task GetAsync_WithMultipleIds_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content"
            );
            await _chunkStore.AddAsync(chunk);

            List<Guid> chunkIds = new List<Guid> { chunk.Id };

            // Act
            IEnumerable<KnowledgeFileChunk> results = await _chunkStore.GetAsync(chunkIds);

            // Assert
            List<KnowledgeFileChunk> resultList = results.ToList();
            Assert.AreEqual(1, resultList.Count);
            Assert.AreEqual(chunk.Id, resultList[0].Id);
        }

        [TestMethod]
        public async Task GetAsync_WithMultipleIds_ShouldHandleLargeNumberOfIds()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            // Create 50 chunks
            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();
            for (int i = 0; i < 50; i++)
            {
                KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                    Guid.NewGuid(),
                    section.Id,
                    i,
                    $"Chunk {i} content"
                );
                chunks.Add(chunk);
                await _chunkStore.AddAsync(chunk);
            }

            List<Guid> chunkIds = chunks.Select(c => c.Id).ToList();

            // Act
            IEnumerable<KnowledgeFileChunk> results = await _chunkStore.GetAsync(chunkIds);

            // Assert
            List<KnowledgeFileChunk> resultList = results.ToList();
            Assert.AreEqual(50, resultList.Count);
            for (int i = 0; i < 50; i++)
            {
                Assert.IsTrue(resultList.Any(c => c.Content == $"Chunk {i} content"));
            }
        }

        [TestMethod]
        public async Task GetAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content"
            );
            await _chunkStore.AddAsync(chunk);

            // Act
            KnowledgeFileChunk? result = await _chunkStore.GetAsync(chunk.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(chunk.Id, result.Id);
        }

        [TestMethod]
        public async Task GetByIndexAsync_ShouldReturnChunk_WhenChunkExists()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                5,
                "Test chunk content",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            await _chunkStore.AddAsync(chunk);

            // Act
            KnowledgeFileChunk? result = await _chunkStore.GetByIndexAsync(section.Id, 5);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(chunk.Id, result.Id);
            Assert.AreEqual(chunk.SectionId, result.SectionId);
            Assert.AreEqual(chunk.ChunkIndex, result.ChunkIndex);
            Assert.AreEqual(chunk.Content, result.Content);
            CollectionAssert.AreEqual(chunk.Embedding, result.Embedding);
        }

        [TestMethod]
        public async Task GetByIndexAsync_ShouldReturnNull_WhenChunkDoesNotExist()
        {
            // Arrange
            await _chunkStore.LoadAsync();
            Guid sectionId = Guid.NewGuid();
            int chunkIndex = 999;

            // Act
            KnowledgeFileChunk? result = await _chunkStore.GetByIndexAsync(sectionId, chunkIndex);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetByIndexAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                3,
                "Test chunk content"
            );
            await _chunkStore.AddAsync(chunk);

            // Act
            KnowledgeFileChunk? result = await _chunkStore.GetByIndexAsync(section.Id, 3);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(chunk.Id, result.Id);
        }

        [TestMethod]
        public async Task DeleteByFileAsync_ShouldReturnTrue_WhenChunksExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk1 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Chunk 1"
            );
            KnowledgeFileChunk chunk2 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                1,
                "Chunk 2"
            );
            await _chunkStore.AddAsync(chunk1);
            await _chunkStore.AddAsync(chunk2);

            // Act
            bool result = await _chunkStore.DeleteByFileAsync(file.Id);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNull(await _chunkStore.GetAsync(chunk1.Id));
            Assert.IsNull(await _chunkStore.GetAsync(chunk2.Id));
        }

        [TestMethod]
        public async Task DeleteByFileAsync_ShouldReturnFalse_WhenNoChunksExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();
            Guid fileId = Guid.NewGuid();

            // Act
            bool result = await _chunkStore.DeleteByFileAsync(fileId);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task DeleteByFileAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content"
            );
            await _chunkStore.AddAsync(chunk);

            // Act
            bool result = await _chunkStore.DeleteByFileAsync(file.Id);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task LoadAsync_ShouldInitializeDatabase_WhenCalled()
        {
            // Act
            await _chunkStore.LoadAsync();

            // Assert
            // Should not throw and database should be accessible
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content"
            );
            await _chunkStore.AddAsync(chunk);

            Assert.IsNotNull(await _chunkStore.GetAsync(chunk.Id));
        }

        [TestMethod]
        public async Task LoadAsync_ShouldNotReinitialize_WhenAlreadyInitialized()
        {
            // Arrange
            await _chunkStore.LoadAsync();

            // Act & Assert
            // Should not throw on second call
            await _chunkStore.LoadAsync();
        }

        [TestMethod]
        public async Task SaveAsync_ShouldComplete_WhenCalled()
        {
            // Arrange
            await _chunkStore.LoadAsync();

            // Act & Assert
            // Should not throw
            await _chunkStore.SaveAsync();
        }

        [TestMethod]
        public async Task FullWorkflow_ShouldWorkCorrectly()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "workflow.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act - Add sections and chunks
            KnowledgeFileSection section1 = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Section 1"
            );
            KnowledgeFileSection section2 = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                1,
                new List<KnowledgeFileChunk>(),
                "Section 2"
            );
            await _sectionStore.AddAsync(section1);
            await _sectionStore.AddAsync(section2);

            KnowledgeFileChunk chunk1 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section1.Id,
                0,
                "Chunk 1 content",
                new float[] { 0.1f, 0.2f }
            );
            KnowledgeFileChunk chunk2 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section1.Id,
                1,
                "Chunk 2 content",
                new float[] { 0.3f, 0.4f }
            );
            KnowledgeFileChunk chunk3 = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section2.Id,
                0,
                "Chunk 3 content",
                new float[] { 0.5f, 0.6f }
            );

            await _chunkStore.AddAsync(chunk1);
            await _chunkStore.AddAsync(chunk2);
            await _chunkStore.AddAsync(chunk3);

            // Assert - Chunks should be retrievable
            KnowledgeFileChunk? retrieved1 = await _chunkStore.GetAsync(chunk1.Id);
            KnowledgeFileChunk? retrieved2 = await _chunkStore.GetAsync(chunk2.Id);
            KnowledgeFileChunk? retrieved3 = await _chunkStore.GetAsync(chunk3.Id);

            Assert.IsNotNull(retrieved1);
            Assert.IsNotNull(retrieved2);
            Assert.IsNotNull(retrieved3);
            Assert.AreEqual("Chunk 1 content", retrieved1.Content);
            Assert.AreEqual("Chunk 2 content", retrieved2.Content);
            Assert.AreEqual("Chunk 3 content", retrieved3.Content);

            // Act - Get by index
            KnowledgeFileChunk? byIndex1 = await _chunkStore.GetByIndexAsync(section1.Id, 0);
            KnowledgeFileChunk? byIndex2 = await _chunkStore.GetByIndexAsync(section1.Id, 1);
            KnowledgeFileChunk? byIndex3 = await _chunkStore.GetByIndexAsync(section2.Id, 0);

            Assert.IsNotNull(byIndex1);
            Assert.IsNotNull(byIndex2);
            Assert.IsNotNull(byIndex3);
            Assert.AreEqual(chunk1.Id, byIndex1.Id);
            Assert.AreEqual(chunk2.Id, byIndex2.Id);
            Assert.AreEqual(chunk3.Id, byIndex3.Id);

            // Act - Delete by file
            bool deleted = await _chunkStore.DeleteByFileAsync(file.Id);

            // Assert - Chunks should be deleted
            Assert.IsTrue(deleted);
            Assert.IsNull(await _chunkStore.GetAsync(chunk1.Id));
            Assert.IsNull(await _chunkStore.GetAsync(chunk2.Id));
            Assert.IsNull(await _chunkStore.GetAsync(chunk3.Id));
        }

        [TestMethod]
        public async Task Chunk_ShouldHandleNullEmbedding()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content",
                null // null embedding
            );
            await _chunkStore.AddAsync(chunk);

            // Act
            KnowledgeFileChunk? retrieved = await _chunkStore.GetAsync(chunk.Id);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.IsNull(retrieved.Embedding);
        }

        [TestMethod]
        public async Task Chunk_ShouldHandleLargeEmbeddings()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            float[] largeEmbedding = new float[1000];
            for (int i = 0; i < largeEmbedding.Length; i++)
            {
                largeEmbedding[i] = (float)i / 1000f;
            }

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Large embedding test",
                largeEmbedding
            );
            await _chunkStore.AddAsync(chunk);

            // Act
            KnowledgeFileChunk? retrieved = await _chunkStore.GetAsync(chunk.Id);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.IsNotNull(retrieved.Embedding);
            Assert.AreEqual(largeEmbedding.Length, retrieved.Embedding.Length);
            CollectionAssert.AreEqual(largeEmbedding, retrieved.Embedding);
        }

        [TestMethod]
        public async Task Chunk_ShouldHandleLongContent()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            string longContent = new string('x', 10000); // 10K character content

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                longContent
            );
            await _chunkStore.AddAsync(chunk);

            // Act
            KnowledgeFileChunk? retrieved = await _chunkStore.GetAsync(chunk.Id);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(longContent, retrieved.Content);
        }
    }
}
