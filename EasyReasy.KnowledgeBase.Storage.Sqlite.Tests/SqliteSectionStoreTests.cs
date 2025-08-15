using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite.Tests
{
    [TestClass]
    public sealed class SqliteSectionStoreTests
    {
        private string _testDbPath = string.Empty;
        private SqliteSectionStore _sectionStore = null!;
        private SqliteFileStore _fileStore = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Use a temporary file for testing
            _testDbPath = Path.GetTempFileName();
            _fileStore = new SqliteFileStore($"Data Source={_testDbPath}");
            _sectionStore = new SqliteSectionStore($"Data Source={_testDbPath}");
        }

        [TestCleanup]
        public void TestCleanup()
        {
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
            Assert.ThrowsException<ArgumentNullException>(() => new SqliteSectionStore(null!));
        }

        [TestMethod]
        public void Constructor_ShouldAccept_WhenConnectionStringIsValid()
        {
            // Act & Assert
            SqliteSectionStore store = new SqliteSectionStore("Data Source=:memory:");
            Assert.IsNotNull(store);
        }

        [TestMethod]
        public async Task AddAsync_ShouldAddSection_WhenValidSectionProvided()
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
                "Test summary",
                new float[] { 0.1f, 0.2f, 0.3f }
            )
            {
                AdditionalContext = "Additional context"
            };

            // Act
            await _sectionStore.AddAsync(section);

            // Assert
            KnowledgeFileSection? retrieved = await _sectionStore.GetAsync(section.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(section.Id, retrieved.Id);
            Assert.AreEqual(section.FileId, retrieved.FileId);
            Assert.AreEqual(section.SectionIndex, retrieved.SectionIndex);
            Assert.AreEqual(section.Summary, retrieved.Summary);
            Assert.AreEqual(section.AdditionalContext, retrieved.AdditionalContext);
            CollectionAssert.AreEqual(section.Embedding, retrieved.Embedding);
        }

        [TestMethod]
        public async Task AddAsync_ShouldThrow_WhenSectionIsNull()
        {
            // Arrange
            await _sectionStore.LoadAsync();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _sectionStore.AddAsync(null!));
        }

        [TestMethod]
        public async Task AddAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );

            // Act
            await _sectionStore.AddAsync(section);

            // Assert
            KnowledgeFileSection? retrieved = await _sectionStore.GetAsync(section.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(section.Id, retrieved.Id);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnSection_WhenSectionExists()
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
                "Test summary",
                new float[] { 0.1f, 0.2f, 0.3f }
            )
            {
                AdditionalContext = "Additional context"
            };
            await _sectionStore.AddAsync(section);

            // Act
            KnowledgeFileSection? result = await _sectionStore.GetAsync(section.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(section.Id, result.Id);
            Assert.AreEqual(section.FileId, result.FileId);
            Assert.AreEqual(section.SectionIndex, result.SectionIndex);
            Assert.AreEqual(section.Summary, result.Summary);
            Assert.AreEqual(section.AdditionalContext, result.AdditionalContext);
            CollectionAssert.AreEqual(section.Embedding, result.Embedding);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnNull_WhenSectionDoesNotExist()
        {
            // Arrange
            await _sectionStore.LoadAsync();
            Guid nonExistentId = Guid.NewGuid();

            // Act
            KnowledgeFileSection? result = await _sectionStore.GetAsync(nonExistentId);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();

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

            // Act
            KnowledgeFileSection? result = await _sectionStore.GetAsync(section.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(section.Id, result.Id);
        }

        [TestMethod]
        public async Task GetByIndexAsync_ShouldReturnSection_WhenSectionExists()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                5,
                new List<KnowledgeFileChunk>(),
                "Test summary",
                new float[] { 0.1f, 0.2f, 0.3f }
            )
            {
                AdditionalContext = "Additional context"
            };
            await _sectionStore.AddAsync(section);

            // Act
            KnowledgeFileSection? result = await _sectionStore.GetByIndexAsync(file.Id, 5);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(section.Id, result.Id);
            Assert.AreEqual(section.FileId, result.FileId);
            Assert.AreEqual(section.SectionIndex, result.SectionIndex);
            Assert.AreEqual(section.Summary, result.Summary);
            Assert.AreEqual(section.AdditionalContext, result.AdditionalContext);
            CollectionAssert.AreEqual(section.Embedding, result.Embedding);
        }

        [TestMethod]
        public async Task GetByIndexAsync_ShouldReturnNull_WhenSectionDoesNotExist()
        {
            // Arrange
            await _sectionStore.LoadAsync();
            Guid fileId = Guid.NewGuid();
            int sectionIndex = 999;

            // Act
            KnowledgeFileSection? result = await _sectionStore.GetByIndexAsync(fileId, sectionIndex);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetByIndexAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                3,
                new List<KnowledgeFileChunk>(),
                "Test summary"
            );
            await _sectionStore.AddAsync(section);

            // Act
            KnowledgeFileSection? result = await _sectionStore.GetByIndexAsync(file.Id, 3);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(section.Id, result.Id);
        }

        [TestMethod]
        public async Task DeleteByFileAsync_ShouldReturnTrue_WhenSectionsExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

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

            // Act
            bool result = await _sectionStore.DeleteByFileAsync(file.Id);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNull(await _sectionStore.GetAsync(section1.Id));
            Assert.IsNull(await _sectionStore.GetAsync(section2.Id));
        }

        [TestMethod]
        public async Task DeleteByFileAsync_ShouldReturnFalse_WhenNoSectionsExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            Guid fileId = Guid.NewGuid();

            // Act
            bool result = await _sectionStore.DeleteByFileAsync(fileId);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task DeleteByFileAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();

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

            // Act
            bool result = await _sectionStore.DeleteByFileAsync(file.Id);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task LoadAsync_ShouldInitializeDatabase_WhenCalled()
        {
            // Act
            await _sectionStore.LoadAsync();

            // Assert
            // Should not throw and database should be accessible
            await _fileStore.LoadAsync();
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

            Assert.IsNotNull(await _sectionStore.GetAsync(section.Id));
        }

        [TestMethod]
        public async Task LoadAsync_ShouldNotReinitialize_WhenAlreadyInitialized()
        {
            // Arrange
            await _sectionStore.LoadAsync();

            // Act & Assert
            // Should not throw on second call
            await _sectionStore.LoadAsync();
        }

        [TestMethod]
        public async Task SaveAsync_ShouldComplete_WhenCalled()
        {
            // Arrange
            await _sectionStore.LoadAsync();

            // Act & Assert
            // Should not throw
            await _sectionStore.SaveAsync();
        }

        [TestMethod]
        public async Task FullWorkflow_ShouldWorkCorrectly()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "workflow.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act - Add sections
            KnowledgeFileSection section1 = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Section 1",
                new float[] { 0.1f, 0.2f }
            )
            {
                AdditionalContext = "Context 1"
            };

            KnowledgeFileSection section2 = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                1,
                new List<KnowledgeFileChunk>(),
                "Section 2",
                new float[] { 0.3f, 0.4f }
            )
            {
                AdditionalContext = "Context 2"
            };

            await _sectionStore.AddAsync(section1);
            await _sectionStore.AddAsync(section2);

            // Assert - Sections should be retrievable
            KnowledgeFileSection? retrieved1 = await _sectionStore.GetAsync(section1.Id);
            KnowledgeFileSection? retrieved2 = await _sectionStore.GetAsync(section2.Id);

            Assert.IsNotNull(retrieved1);
            Assert.IsNotNull(retrieved2);
            Assert.AreEqual("Section 1", retrieved1.Summary);
            Assert.AreEqual("Section 2", retrieved2.Summary);

            // Act - Get by index
            KnowledgeFileSection? byIndex1 = await _sectionStore.GetByIndexAsync(file.Id, 0);
            KnowledgeFileSection? byIndex2 = await _sectionStore.GetByIndexAsync(file.Id, 1);

            Assert.IsNotNull(byIndex1);
            Assert.IsNotNull(byIndex2);
            Assert.AreEqual(section1.Id, byIndex1.Id);
            Assert.AreEqual(section2.Id, byIndex2.Id);

            // Act - Delete by file
            bool deleted = await _sectionStore.DeleteByFileAsync(file.Id);

            // Assert - Sections should be deleted
            Assert.IsTrue(deleted);
            Assert.IsNull(await _sectionStore.GetAsync(section1.Id));
            Assert.IsNull(await _sectionStore.GetAsync(section2.Id));
        }

        [TestMethod]
        public async Task Section_ShouldHandleNullValues()
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
                null, // null summary
                null  // null embedding
            )
            {
                AdditionalContext = null // null additional context
            };
            await _sectionStore.AddAsync(section);

            // Act
            KnowledgeFileSection? retrieved = await _sectionStore.GetAsync(section.Id);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.IsNull(retrieved.Summary);
            Assert.IsNull(retrieved.AdditionalContext);
            Assert.IsNull(retrieved.Embedding);
        }

        [TestMethod]
        public async Task Section_ShouldHandleLargeEmbeddings()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            float[] largeEmbedding = new float[1000];
            for (int i = 0; i < largeEmbedding.Length; i++)
            {
                largeEmbedding[i] = (float)i / 1000f;
            }

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Large embedding test",
                largeEmbedding
            );
            await _sectionStore.AddAsync(section);

            // Act
            KnowledgeFileSection? retrieved = await _sectionStore.GetAsync(section.Id);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.IsNotNull(retrieved.Embedding);
            Assert.AreEqual(largeEmbedding.Length, retrieved.Embedding.Length);
            CollectionAssert.AreEqual(largeEmbedding, retrieved.Embedding);
        }
    }
}
