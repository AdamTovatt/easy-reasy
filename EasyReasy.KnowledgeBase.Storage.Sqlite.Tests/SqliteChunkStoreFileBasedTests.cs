using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite.Tests
{
    [TestClass]
    public sealed class SqliteChunkStoreFileBasedTests
    {
        private string _testDbPath = string.Empty;
        private SqliteChunkStore _chunkStore = null!;
        private SqliteFileStore _fileStore = null!;
        private SqliteSectionStore _sectionStore = null!;

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
            KnowledgeFileSection section = KnowledgeFileSection.CreateFromChunks(chunks, fileId, sectionIndex);
            section.Summary = summary;
            return section;
        }

        [TestMethod]
        public async Task Data_ShouldPersist_WhenStoreIsRecreated()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "persistent.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Persistent section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

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
            Assert.IsTrue(retrievedSection.Chunks.Count > 0);

            KnowledgeFileChunk? retrievedChunk = await newChunkStore.GetAsync(retrievedSection.Chunks[0].Id);
            Assert.IsNotNull(retrievedChunk);
            Assert.AreEqual(retrievedSection.Chunks[0].Content, retrievedChunk.Content);
            CollectionAssert.AreEqual(retrievedSection.Chunks[0].Embedding, retrievedChunk.Embedding);
        }

        [TestMethod]
        public async Task MultipleStores_ShouldShareData_WhenUsingSameFile()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "shared.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Shared section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Shared chunk content",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            await _chunkStore.AddAsync(chunk);

            // Act - Create second store instances
            SqliteFileStore secondFileStore = new SqliteFileStore($"Data Source={_testDbPath}");
            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore secondSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", secondChunkStore);
            await secondFileStore.LoadAsync();
            await secondSectionStore.LoadAsync();
            await secondChunkStore.LoadAsync();

            // Assert - Both stores should see the same data
            Assert.IsTrue(await _fileStore.ExistsAsync(file.Id));
            Assert.IsTrue(await secondFileStore.ExistsAsync(file.Id));

            KnowledgeFileChunk? fromFirst = await _chunkStore.GetAsync(chunk.Id);
            KnowledgeFileChunk? fromSecond = await secondChunkStore.GetAsync(chunk.Id);

            Assert.IsNotNull(fromFirst);
            Assert.IsNotNull(fromSecond);
            Assert.AreEqual(fromFirst.Id, fromSecond.Id);
            Assert.AreEqual(fromFirst.Content, fromSecond.Content);
            CollectionAssert.AreEqual(fromFirst.Embedding, fromSecond.Embedding);
        }

        [TestMethod]
        public async Task Updates_ShouldBeVisible_AcrossStoreInstances()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "original.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            Guid sectionId = Guid.NewGuid();
            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();
            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                sectionId,
                0,
                "Original chunk content",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            chunks.Add(chunk);

            KnowledgeFileSection section = new KnowledgeFileSection(
                sectionId,
                file.Id,
                0,
                chunks,
                "Original section"
            );
            await _sectionStore.AddAsync(section);
            await _chunkStore.AddAsync(chunk);

            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            await secondChunkStore.LoadAsync();

            // Act - Delete from second store (simulating an update scenario)
            bool deleted = await secondChunkStore.DeleteByFileAsync(file.Id);

            // Assert - First store should see the deletion
            Assert.IsTrue(deleted);
            Assert.IsNull(await _chunkStore.GetAsync(chunk.Id));
        }

        [TestMethod]
        public async Task Deletes_ShouldBeVisible_AcrossStoreInstances()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "to-delete.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            Guid sectionId = Guid.NewGuid();
            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();
            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                sectionId,
                0,
                "To delete chunk content"
            );
            chunks.Add(chunk);

            KnowledgeFileSection section = new KnowledgeFileSection(
                sectionId,
                file.Id,
                0,
                chunks,
                "To delete section"
            );
            await _sectionStore.AddAsync(section);
            await _chunkStore.AddAsync(chunk);

            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            await secondChunkStore.LoadAsync();

            // Act - Delete from second store
            bool deleted = await secondChunkStore.DeleteByFileAsync(file.Id);

            // Assert - First store should see the deletion
            Assert.IsTrue(deleted);
            Assert.IsNull(await _chunkStore.GetAsync(chunk.Id));
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
            await sectionStore.LoadAsync();
            await chunkStore.LoadAsync();

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
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Test section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

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
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Test section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test chunk content"
            );
            await _chunkStore.AddAsync(chunk);
            await _chunkStore.DeleteByFileAsync(file.Id);

            // Assert - Database should still be valid and accessible
            Assert.IsTrue(File.Exists(_testDbPath));

            // Create new stores to verify database integrity
            SqliteFileStore newFileStore = new SqliteFileStore($"Data Source={_testDbPath}");
            SqliteChunkStore newChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            SqliteSectionStore newSectionStore = new SqliteSectionStore($"Data Source={_testDbPath}", newChunkStore);
            await newFileStore.LoadAsync();
            await newSectionStore.LoadAsync();
            await newChunkStore.LoadAsync();

            // Should be able to add new data
            KnowledgeFile newFile = new KnowledgeFile(Guid.NewGuid(), "new.txt", new byte[] { 5, 6, 7, 8 });
            await newFileStore.AddAsync(newFile);

            KnowledgeFileSection newSection = await CreateValidSectionAsync(newFile.Id, 0, "New section");
            await newSectionStore.AddAsync(newSection);

            foreach (KnowledgeFileChunk sectionChunk in newSection.Chunks)
            {
                await newChunkStore.AddAsync(sectionChunk);
            }

            KnowledgeFileChunk newChunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                newSection.Id,
                0,
                "New chunk content"
            );
            await newChunkStore.AddAsync(newChunk);

            Assert.IsTrue(await newFileStore.ExistsAsync(newFile.Id));
            Assert.IsNotNull(await newSectionStore.GetAsync(newSection.Id));
            Assert.IsNotNull(await newChunkStore.GetAsync(newChunk.Id));
        }

        [TestMethod]
        public async Task MultipleStores_ShouldHandleConcurrentWrites()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "concurrent.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Concurrent section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            await secondChunkStore.LoadAsync();

            // Act - Add chunks from both stores concurrently
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                        Guid.NewGuid(),
                        section.Id,
                        index,
                        $"Concurrent chunk {index} content",
                        new float[] { (float)index, (float)(index + 1) }
                    );
                    await (index % 2 == 0 ? _chunkStore : secondChunkStore).AddAsync(chunk);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All chunks should be added successfully
            for (int i = 0; i < 5; i++)
            {
                KnowledgeFileChunk? chunk = await _chunkStore.GetByIndexAsync(section.Id, i);
                Assert.IsNotNull(chunk, $"Chunk at index {i} should exist");
            }
        }

        [TestMethod]
        public async Task MultipleStores_ShouldHandleConcurrentReads()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "concurrent-read.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Concurrent read section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Concurrent read chunk content",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            await _chunkStore.AddAsync(chunk);

            SqliteChunkStore secondChunkStore = new SqliteChunkStore($"Data Source={_testDbPath}");
            await secondChunkStore.LoadAsync();

            // Act - Read from both stores concurrently
            List<Task<KnowledgeFileChunk?>> tasks = new List<Task<KnowledgeFileChunk?>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () => await (i % 2 == 0 ? _chunkStore : secondChunkStore).GetAsync(chunk.Id)));
            }

            KnowledgeFileChunk?[] results = await Task.WhenAll(tasks);

            // Assert - All reads should return the same data
            Assert.AreEqual(10, results.Length);
            foreach (KnowledgeFileChunk? result in results)
            {
                Assert.IsNotNull(result);
                Assert.AreEqual(chunk.Id, result.Id);
                Assert.AreEqual(chunk.Content, result.Content);
                CollectionAssert.AreEqual(chunk.Embedding, result.Embedding);
            }
        }

        [TestMethod]
        public async Task Store_ShouldHandleLargeEmbeddings()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "large-embedding.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Large embedding section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            float[] largeEmbedding = new float[10000]; // 10K embedding
            for (int i = 0; i < largeEmbedding.Length; i++)
            {
                largeEmbedding[i] = (float)i / 10000f;
            }

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Large embedding chunk content",
                largeEmbedding
            );

            // Act
            await _chunkStore.AddAsync(chunk);

            // Assert
            KnowledgeFileChunk? retrieved = await _chunkStore.GetAsync(chunk.Id);
            Assert.IsNotNull(retrieved);
            Assert.IsNotNull(retrieved.Embedding);
            Assert.AreEqual(largeEmbedding.Length, retrieved.Embedding.Length);
            CollectionAssert.AreEqual(largeEmbedding, retrieved.Embedding);
        }

        [TestMethod]
        public async Task Store_ShouldHandleManyChunks()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "many-chunks.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Many chunks section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            const int chunkCount = 100;

            // Act - Add many chunks
            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();
            for (int i = 0; i < chunkCount; i++)
            {
                KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                    Guid.NewGuid(),
                    section.Id,
                    i,
                    $"Chunk {i} content",
                    new float[] { (float)i, (float)(i + 1) }
                );
                await _chunkStore.AddAsync(chunk);
                chunks.Add(chunk);
            }

            // Assert - All chunks should be retrievable
            foreach (KnowledgeFileChunk chunk in chunks)
            {
                KnowledgeFileChunk? retrieved = await _chunkStore.GetAsync(chunk.Id);
                Assert.IsNotNull(retrieved);
                Assert.AreEqual(chunk.Content, retrieved.Content);
            }

            // Verify by index retrieval
            for (int i = 0; i < chunkCount; i++)
            {
                KnowledgeFileChunk? retrieved = await _chunkStore.GetByIndexAsync(section.Id, i);
                Assert.IsNotNull(retrieved);
                Assert.AreEqual(i, retrieved.ChunkIndex);
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
            await chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "missing-file.txt", new byte[] { 1, 2, 3, 4 });
            await fileStore.AddAsync(file);

            // Create a valid section with chunks
            Guid sectionId = Guid.NewGuid();
            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();
            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                sectionId,
                0,
                "Missing file chunk content"
            );
            chunks.Add(chunk);

            KnowledgeFileSection section = KnowledgeFileSection.CreateFromChunks(chunks, file.Id, 0);
            await sectionStore.AddAsync(section);
            await chunkStore.AddAsync(chunk);

            Assert.IsTrue(await fileStore.ExistsAsync(file.Id));
            Assert.IsNotNull(await sectionStore.GetAsync(section.Id));
            Assert.IsNotNull(await chunkStore.GetAsync(chunk.Id));

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
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "foreign-key.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Foreign key section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Foreign key chunk content"
            );
            await _chunkStore.AddAsync(chunk);

            // Act - Delete the file
            bool fileDeleted = await _fileStore.DeleteAsync(file.Id);

            // Assert - File should be deleted and chunk should be cascade deleted
            Assert.IsTrue(fileDeleted);
            Assert.IsFalse(await _fileStore.ExistsAsync(file.Id));
            Assert.IsNull(await _sectionStore.GetAsync(section.Id));
            Assert.IsNull(await _chunkStore.GetAsync(chunk.Id));
        }

        [TestMethod]
        public async Task ForeignKeyConstraint_ShouldWork_WithSectionDeletion()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "section-deletion.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Section deletion section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Section deletion chunk content"
            );
            await _chunkStore.AddAsync(chunk);

            // Act - Delete the section
            bool sectionDeleted = await _sectionStore.DeleteByFileAsync(file.Id);

            // Assert - Section should be deleted and chunk should be cascade deleted
            Assert.IsTrue(sectionDeleted);
            Assert.IsNull(await _sectionStore.GetAsync(section.Id));
            Assert.IsNull(await _chunkStore.GetAsync(chunk.Id));
        }

        [TestMethod]
        public async Task Store_ShouldHandleLongContent()
        {
            // Arrange
            await _fileStore.LoadAsync();
            await _sectionStore.LoadAsync();
            await _chunkStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "long-content.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFileSection section = await CreateValidSectionAsync(file.Id, 0, "Long content section");
            await _sectionStore.AddAsync(section);

            foreach (KnowledgeFileChunk sectionChunk in section.Chunks)
            {
                await _chunkStore.AddAsync(sectionChunk);
            }

            string longContent = new string('x', 50000); // 50K character content

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                longContent
            );

            // Act
            await _chunkStore.AddAsync(chunk);

            // Assert
            KnowledgeFileChunk? retrieved = await _chunkStore.GetAsync(chunk.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(longContent, retrieved.Content);
        }
    }
}
