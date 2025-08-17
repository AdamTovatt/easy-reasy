using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite.Tests
{
    [TestClass]
    public sealed class SqliteSectionStoreFileBasedTests
    {
        private string _testDbPath = string.Empty;
        private SqliteSectionStore _sectionStore = null!;
        private SqliteFileStore _fileStore = null!;
        private SqliteChunkStore _chunkStore = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Use a temporary file for testing persistence
            _testDbPath = Path.GetTempFileName();
            _fileStore = new SqliteFileStore($"Data Source={_testDbPath}");
            _chunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            _sectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", _chunkStore);
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

        /// <summary>
        /// Helper method to create a valid section with chunks for testing.
        /// </summary>
        private async Task<KnowledgeFileSection> CreateValidSectionAsync(Guid fileId, int sectionIndex, string summary = "Test section")
        {
            await Task.CompletedTask;

            Guid sectionId = Guid.NewGuid();
            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                sectionId,
                0,
                $"Test chunk content for section {sectionIndex}",
                new float[] { 0.1f, 0.2f, 0.3f }
            );

            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk> { chunk };

            // Create section using the CreateFromChunks method
            KnowledgeFileSection result = KnowledgeFileSection.CreateFromChunks(chunks, fileId, sectionIndex);
            result.Summary = summary;
            return result;
        }

        [TestMethod]
        public async Task Data_ShouldPersist_WhenStoreIsRecreated()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _chunkStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "persistent.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            Guid sectionId = Guid.NewGuid();
            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                sectionId,
                0,
                "Persistent chunk content",
                new float[] { 0.1f, 0.2f, 0.3f }
            );

            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk> { chunk };

            KnowledgeFileSection section = new KnowledgeFileSection(
                sectionId,
                file.Id,
                0,
                chunks,
                "Persistent section",
                new float[] { 0.1f, 0.2f, 0.3f }
            )
            {
                AdditionalContext = "Persistent context"
            };

            await _sectionStore.AddAsync(section);
            await _chunkStore.AddAsync(chunk);

            // Act - Create new store instances with the same database file
            SqliteFileStore newFileStore = new SqliteFileStore($"Data Source={_testDbPath}");
            SqliteChunkStore newChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore newSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", newChunkStore);
            await newFileStore.LoadAsync();
            await newChunkStore.LoadAsync();
            await newSectionStore.LoadAsync();

            // Assert - Data should still be there
            KnowledgeFile? retrievedFile = await newFileStore.GetAsync(file.Id);
            KnowledgeFileSection? retrievedSection = await newSectionStore.GetAsync(section.Id);

            Assert.IsNotNull(retrievedFile);
            Assert.IsNotNull(retrievedSection);
            Assert.AreEqual(file.Id, retrievedFile.Id);
            Assert.AreEqual(section.Id, retrievedSection.Id);
            Assert.AreEqual(section.Summary, retrievedSection.Summary);
            Assert.AreEqual(section.AdditionalContext, retrievedSection.AdditionalContext);
            CollectionAssert.AreEqual(section.Embedding, retrievedSection.Embedding);
        }

        [TestMethod]
        public async Task MultipleStores_ShouldShareData_WhenUsingSameFile()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "shared.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Shared section");
            section.AdditionalContext = "Shared context";
            section.Embedding = new float[] { 0.1f, 0.2f, 0.3f };
            await _sectionStore.AddAsync(section);
            
            foreach (KnowledgeFileChunk chunk in section.Chunks)
            {
                await _chunkStore.AddAsync(chunk);
            }

            // Act - Create second store instances
            SqliteFileStore secondFileStore = new SqliteFileStore($"Data Source={_testDbPath}");
            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore secondSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", secondChunkStore);
            await secondFileStore.LoadAsync();
            await secondChunkStore.LoadAsync();
            await secondSectionStore.LoadAsync();

            // Assert - Both stores should see the same data
            Assert.IsTrue(await _fileStore.ExistsAsync(file.Id));
            Assert.IsTrue(await secondFileStore.ExistsAsync(file.Id));

            KnowledgeFileSection? fromFirst = await _sectionStore.GetAsync(section.Id);
            KnowledgeFileSection? fromSecond = await secondSectionStore.GetAsync(section.Id);

            Assert.IsNotNull(fromFirst);
            Assert.IsNotNull(fromSecond);
            Assert.AreEqual(fromFirst.Id, fromSecond.Id);
            Assert.AreEqual(fromFirst.Summary, fromSecond.Summary);
            Assert.AreEqual(fromFirst.AdditionalContext, fromSecond.AdditionalContext);
            CollectionAssert.AreEqual(fromFirst.Embedding, fromSecond.Embedding);
        }

        [TestMethod]
        public async Task Updates_ShouldBeVisible_AcrossStoreInstances()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "original.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Original section");
            section.AdditionalContext = "Original context";
            section.Embedding = new float[] { 0.1f, 0.2f, 0.3f };
            await _sectionStore.AddAsync(section);

            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore secondSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", secondChunkStore);
            await secondChunkStore.LoadAsync();
            await secondSectionStore.LoadAsync();

            // Act - Delete from second store (simulating an update scenario)
            bool deleted = await secondSectionStore.DeleteByFileAsync(file.Id);

            // Assert - First store should see the deletion
            Assert.IsTrue(deleted);
            Assert.IsNull(await _sectionStore.GetAsync(section.Id));
        }

        [TestMethod]
        public async Task Deletes_ShouldBeVisible_AcrossStoreInstances()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _chunkStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "to-delete.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "To delete section");
            await _sectionStore.AddAsync(section);

            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore secondSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", secondChunkStore);
            await secondChunkStore.LoadAsync();
            await secondSectionStore.LoadAsync();

            // Act - Delete from second store
            bool deleted = await secondSectionStore.DeleteByFileAsync(file.Id);

            // Assert - First store should see the deletion
            Assert.IsTrue(deleted);
            Assert.IsNull(await _sectionStore.GetAsync(section.Id));
        }

        [TestMethod]
        public async Task DatabaseFile_ShouldBeCreated_WhenStoreIsInitialized()
        {
            // Arrange
            string dbPath = Path.GetTempFileName();
            File.Delete(dbPath); // Ensure file doesn't exist

            // Act
            SqliteFileStore fileStore = new SqliteFileStore($"Data Source={dbPath}");
            SqliteChunkStore chunkStore = new SqliteChunkStore($"Data Source={dbPath}");
            SqliteSectionStore sectionStore = new SqliteSectionStore($"Data Source={dbPath}", chunkStore);
            await fileStore.LoadAsync();
            await chunkStore.LoadAsync();
            await sectionStore.LoadAsync();

            // Assert
            Assert.IsTrue(File.Exists(dbPath));

            // Cleanup
            try
            {
                File.Delete(dbPath);
            }
            catch { /* ignore cleanup errors */ }
        }

        [TestMethod]
        public async Task DatabaseFile_ShouldContainData_AfterOperations()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _chunkStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Test section");
            section.AdditionalContext = "Test context";
            section.Embedding = new float[] { 0.1f, 0.2f, 0.3f };
            await _sectionStore.AddAsync(section);

            // Assert - File should exist and contain data
            Assert.IsTrue(File.Exists(_testDbPath));
            long fileSize = new FileInfo(_testDbPath).Length;
            Assert.IsTrue(fileSize > 0, "Database file should contain data");
        }

        [TestMethod]
        public async Task DatabaseFile_ShouldBeValid_AfterOperations()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Test section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk chunk in section.Chunks)
            {
                await _chunkStore.AddAsync(chunk);
            }

            await _sectionStore.DeleteByFileAsync(file.Id);

            // Assert - Database should still be valid and accessible
            Assert.IsTrue(File.Exists(_testDbPath));

            // Create new stores to verify database integrity
            SqliteFileStore newFileStore = new SqliteFileStore($"Data Source={_testDbPath}");
            SqliteChunkStore newChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore newSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", newChunkStore);
            await newFileStore.LoadAsync();
            await newSectionStore.LoadAsync();

            // Should be able to add new data
            KnowledgeFile newFile = new KnowledgeFile(Guid.NewGuid(), "new.txt", new byte[] { 5, 6, 7, 8 });
            await newFileStore.AddAsync(newFile);

            KnowledgeFileSection newSection = await CreateValidSectionAsync(newFile.Id, 0, "New section");
            await newSectionStore.AddAsync(newSection);

            foreach (KnowledgeFileChunk chunk in newSection.Chunks)
            {
                await newChunkStore.AddAsync(chunk);
            }

            Assert.IsTrue(await newFileStore.ExistsAsync(newFile.Id));
            Assert.IsNotNull(await newSectionStore.GetAsync(newSection.Id));
        }

        [TestMethod]
        public async Task MultipleStores_ShouldHandleConcurrentWrites()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "concurrent.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore secondSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", secondChunkStore);
            await secondSectionStore.LoadAsync();

            // Act - Add sections from both stores concurrently
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, index, $"Concurrent section {index}");
                    section.Embedding = new float[] { (float)index, (float)(index + 1) };
                    await (index % 2 == 0 ? _sectionStore : secondSectionStore).AddAsync(section);

                    foreach (KnowledgeFileChunk chunk in section.Chunks)
                    {
                        await (index % 2 == 0 ? _chunkStore : secondChunkStore).AddAsync(chunk);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All sections should be added successfully
            for (int i = 0; i < 5; i++)
            {
                KnowledgeFileSection? section = await _sectionStore.GetByIndexAsync(file.Id, i);
                Assert.IsNotNull(section, $"Section at index {i} should exist");
            }
        }

        [TestMethod]
        public async Task MultipleStores_ShouldHandleConcurrentReads()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "concurrent-read.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Concurrent read section");
            section.AdditionalContext = "Concurrent context";
            section.Embedding = new float[] { 0.1f, 0.2f, 0.3f };
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk chunk in section.Chunks)
            {
                await _chunkStore.AddAsync(chunk);
            }

            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore secondSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", secondChunkStore);
            await secondSectionStore.LoadAsync();

            // Act - Read from both stores concurrently
            List<Task<KnowledgeFileSection?>> tasks = new List<Task<KnowledgeFileSection?>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () => await (i % 2 == 0 ? _sectionStore : secondSectionStore).GetAsync(section.Id)));
            }

            KnowledgeFileSection?[] results = await Task.WhenAll(tasks);

            // Assert - All reads should return the same data
            Assert.AreEqual(10, results.Length);
            foreach (KnowledgeFileSection? result in results)
            {
                Assert.IsNotNull(result);
                Assert.AreEqual(section.Id, result.Id);
                Assert.AreEqual(section.Summary, result.Summary);
                Assert.AreEqual(section.AdditionalContext, result.AdditionalContext);
                CollectionAssert.AreEqual(section.Embedding, result.Embedding);
            }
        }

        [TestMethod]
        public async Task Store_ShouldHandleLargeEmbeddings()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "large-embedding.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            float[] largeEmbedding = new float[10000]; // 10K embedding
            for (int i = 0; i < largeEmbedding.Length; i++)
            {
                largeEmbedding[i] = (float)i / 10000f;
            }

            Guid sectionId = Guid.NewGuid();

            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();
            chunks.Add(new KnowledgeFileChunk(Guid.NewGuid(), sectionId, 0, "Test", new float[] { 1, 2, 3, 4 }));

            KnowledgeFileSection section = new KnowledgeFileSection(
                sectionId,
                file.Id,
                0,
                chunks,
                "Large embedding section",
                largeEmbedding
            )
            {
                AdditionalContext = "Large embedding context"
            };

            // Act
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk chunk in chunks)
            {
                await _chunkStore.AddAsync(chunk);
            }

            // Assert
            KnowledgeFileSection? retrieved = await _sectionStore.GetAsync(section.Id);
            Assert.IsNotNull(retrieved);
            Assert.IsNotNull(retrieved.Embedding);
            Assert.AreEqual(largeEmbedding.Length, retrieved.Embedding.Length);
            CollectionAssert.AreEqual(largeEmbedding, retrieved.Embedding);
        }

        [TestMethod]
        public async Task Store_ShouldHandleManySections()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "many-sections.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            const int sectionCount = 100;

            // Act - Add many sections
            List<KnowledgeFileSection> sections = new List<KnowledgeFileSection>();
            for (int i = 0; i < sectionCount; i++)
            {
                KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, i, "Summary " + i);
                section.AdditionalContext = $"Context {i}";
                await _sectionStore.AddAsync(section);
                sections.Add(section);

                foreach (KnowledgeFileChunk chunk in section.Chunks)
                {
                    await _chunkStore.AddAsync(chunk);
                }
            }

            // Assert - All sections should be retrievable
            foreach (KnowledgeFileSection section in sections)
            {
                KnowledgeFileSection? retrieved = await _sectionStore.GetAsync(section.Id);
                Assert.IsNotNull(retrieved);
                Assert.AreEqual(section.Summary, retrieved.Summary);
                Assert.AreEqual(section.AdditionalContext, retrieved.AdditionalContext);
            }

            // Verify by index retrieval
            for (int i = 0; i < sectionCount; i++)
            {
                KnowledgeFileSection? retrieved = await _sectionStore.GetByIndexAsync(file.Id, i);
                Assert.IsNotNull(retrieved);
                Assert.AreEqual(i, retrieved.SectionIndex);
            }
        }

        [TestMethod]
        public async Task Store_ShouldHandleDatabaseRecreation_WhenFileIsMissing()
        {
            // Arrange
            string missingDbPath = Path.GetTempFileName();
            File.Delete(missingDbPath); // Ensure file doesn't exist

            // Act & Assert - Should work with missing file
            SqliteFileStore fileStore = new SqliteFileStore($"Data Source={missingDbPath}");
            SqliteChunkStore chunkStore = new SqliteChunkStore($"Data Source={missingDbPath}");
            SqliteSectionStore sectionStore = new SqliteSectionStore($"Data Source={missingDbPath}", chunkStore);
            await fileStore.LoadAsync();
            await sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "missing-file.txt", new byte[] { 1, 2, 3, 4 });
            await fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Missing file section");
            await sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk chunk in section.Chunks)
            {
                await chunkStore.AddAsync(chunk);
            }

            Assert.IsTrue(await fileStore.ExistsAsync(file.Id));
            Assert.IsNotNull(await sectionStore.GetAsync(section.Id));

            // Cleanup
            try
            {
                File.Delete(missingDbPath);
            }
            catch { /* ignore cleanup errors */ }
        }

        [TestMethod]
        public async Task ForeignKeyConstraint_ShouldWork_WithFileDeletion()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "foreign-key.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Foreign key section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk chunk in section.Chunks)
            {
                await _chunkStore.AddAsync(chunk);
            }

            // Act - Delete the file
            bool fileDeleted = await _fileStore.DeleteAsync(file.Id);

            // Assert - File should be deleted and section should be cascade deleted
            Assert.IsTrue(fileDeleted);
            Assert.IsFalse(await _fileStore.ExistsAsync(file.Id));
            Assert.IsNull(await _sectionStore.GetAsync(section.Id));
        }
    }
}
