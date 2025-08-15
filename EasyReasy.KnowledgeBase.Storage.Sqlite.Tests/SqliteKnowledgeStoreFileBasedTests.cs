using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite.Tests
{
    [TestClass]
    public sealed class SqliteKnowledgeStoreFileBasedTests
    {
        private string _testDbPath = string.Empty;
        private SqliteKnowledgeStore _knowledgeStore = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Use a temporary file for testing persistence
            _testDbPath = Path.GetTempFileName();
            _knowledgeStore = new SqliteKnowledgeStore($"Data Source={_testDbPath}");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _knowledgeStore = null!;

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
            Assert.ThrowsException<ArgumentException>(() => new SqliteKnowledgeStore(null!));
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenConnectionStringIsEmpty()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => new SqliteKnowledgeStore(string.Empty));
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenConnectionStringIsWhitespace()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => new SqliteKnowledgeStore("   "));
        }

        [TestMethod]
        public void Constructor_ShouldAccept_WhenConnectionStringIsValid()
        {
            // Act & Assert
            SqliteKnowledgeStore store = new SqliteKnowledgeStore("Data Source=:memory:");
            Assert.IsNotNull(store);
            Assert.IsNotNull(store.Files);
            Assert.IsNotNull(store.Sections);
            Assert.IsNotNull(store.Chunks);
        }

        [TestMethod]
        public async Task LoadAsync_ShouldInitializeAllStores()
        {
            // Act
            await _knowledgeStore.LoadAsync();

            // Assert - All stores should be initialized and ready to use
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _knowledgeStore.Files.AddAsync(file);
            Assert.IsTrue(await _knowledgeStore.Files.ExistsAsync(file.Id));
        }

        [TestMethod]
        public async Task CompleteWorkflow_ShouldPersist_WhenStoreIsRecreated()
        {
            // Arrange
            await _knowledgeStore.LoadAsync();

            // Create a complete knowledge structure
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "document.txt", new byte[] { 1, 2, 3, 4 });
            await _knowledgeStore.Files.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Document summary",
                new float[] { 0.1f, 0.2f, 0.3f }
            )
            {
                AdditionalContext = "Document context"
            };
            await _knowledgeStore.Sections.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Document chunk content",
                new float[] { 0.4f, 0.5f, 0.6f }
            );
            await _knowledgeStore.Chunks.AddAsync(chunk);

            // Act - Create a new store instance with the same database file
            SqliteKnowledgeStore newStore = new SqliteKnowledgeStore($"Data Source={_testDbPath}");
            await newStore.LoadAsync();

            // Assert - All data should still be there
            KnowledgeFile? retrievedFile = await newStore.Files.GetAsync(file.Id);
            KnowledgeFileSection? retrievedSection = await newStore.Sections.GetAsync(section.Id);
            KnowledgeFileChunk? retrievedChunk = await newStore.Chunks.GetAsync(chunk.Id);

            Assert.IsNotNull(retrievedFile);
            Assert.IsNotNull(retrievedSection);
            Assert.IsNotNull(retrievedChunk);

            Assert.AreEqual(file.Id, retrievedFile.Id);
            Assert.AreEqual(file.Name, retrievedFile.Name);
            CollectionAssert.AreEqual(file.Hash, retrievedFile.Hash);

            Assert.AreEqual(section.Id, retrievedSection.Id);
            Assert.AreEqual(section.FileId, retrievedSection.FileId);
            Assert.AreEqual(section.Summary, retrievedSection.Summary);
            Assert.AreEqual(section.AdditionalContext, retrievedSection.AdditionalContext);
            CollectionAssert.AreEqual(section.Embedding, retrievedSection.Embedding);

            Assert.AreEqual(chunk.Id, retrievedChunk.Id);
            Assert.AreEqual(chunk.SectionId, retrievedChunk.SectionId);
            Assert.AreEqual(chunk.Content, retrievedChunk.Content);
            CollectionAssert.AreEqual(chunk.Embedding, retrievedChunk.Embedding);
        }

        [TestMethod]
        public async Task MultipleStores_ShouldShareData_WhenUsingSameFile()
        {
            // Arrange
            await _knowledgeStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "shared.txt", new byte[] { 1, 2, 3, 4 });
            await _knowledgeStore.Files.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Shared section",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            await _knowledgeStore.Sections.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Shared chunk",
                new float[] { 0.4f, 0.5f, 0.6f }
            );
            await _knowledgeStore.Chunks.AddAsync(chunk);

            // Act - Create a second store instance
            SqliteKnowledgeStore secondStore = new SqliteKnowledgeStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Assert - Both stores should see the same data
            Assert.IsTrue(await _knowledgeStore.Files.ExistsAsync(file.Id));
            Assert.IsTrue(await secondStore.Files.ExistsAsync(file.Id));

            // Check sections exist by retrieving them
            KnowledgeFileSection? retrievedSection1 = await _knowledgeStore.Sections.GetAsync(section.Id);
            KnowledgeFileSection? retrievedSection2 = await secondStore.Sections.GetAsync(section.Id);
            Assert.IsNotNull(retrievedSection1);
            Assert.IsNotNull(retrievedSection2);

            // Check chunks exist by retrieving them
            KnowledgeFileChunk? retrievedChunk1 = await _knowledgeStore.Chunks.GetAsync(chunk.Id);
            KnowledgeFileChunk? retrievedChunk2 = await secondStore.Chunks.GetAsync(chunk.Id);
            Assert.IsNotNull(retrievedChunk1);
            Assert.IsNotNull(retrievedChunk2);
        }

        [TestMethod]
        public async Task Updates_ShouldBeVisible_AcrossStoreInstances()
        {
            // Arrange
            await _knowledgeStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "original.txt", new byte[] { 1, 2, 3 });
            await _knowledgeStore.Files.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Original summary",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            await _knowledgeStore.Sections.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Original content",
                new float[] { 0.4f, 0.5f, 0.6f }
            );
            await _knowledgeStore.Chunks.AddAsync(chunk);

            SqliteKnowledgeStore secondStore = new SqliteKnowledgeStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Act - Update from second store
            KnowledgeFile updatedFile = new KnowledgeFile(file.Id, "updated.txt", new byte[] { 4, 5, 6 });
            await secondStore.Files.UpdateAsync(updatedFile);

            // Note: Sections and Chunks don't have UpdateAsync methods in the interface
            // We'll test file updates only for now

            // Assert - First store should see file updates
            KnowledgeFile? retrievedFile = await _knowledgeStore.Files.GetAsync(file.Id);
            KnowledgeFileSection? retrievedSection = await _knowledgeStore.Sections.GetAsync(section.Id);
            KnowledgeFileChunk? retrievedChunk = await _knowledgeStore.Chunks.GetAsync(chunk.Id);

            Assert.IsNotNull(retrievedFile);
            Assert.AreEqual("updated.txt", retrievedFile.Name);
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, retrievedFile.Hash);

            // Sections and chunks should remain unchanged since they don't have UpdateAsync
            Assert.IsNotNull(retrievedSection);
            Assert.AreEqual("Original summary", retrievedSection.Summary);

            Assert.IsNotNull(retrievedChunk);
            Assert.AreEqual("Original content", retrievedChunk.Content);
        }

        [TestMethod]
        public async Task Deletes_ShouldBeVisible_AcrossStoreInstances()
        {
            // Arrange
            await _knowledgeStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "to-delete.txt", new byte[] { 1, 2, 3 });
            await _knowledgeStore.Files.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "To delete section",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            await _knowledgeStore.Sections.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "To delete chunk",
                new float[] { 0.4f, 0.5f, 0.6f }
            );
            await _knowledgeStore.Chunks.AddAsync(chunk);

            SqliteKnowledgeStore secondStore = new SqliteKnowledgeStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Act - Delete from second store
            bool fileDeleted = await secondStore.Files.DeleteAsync(file.Id);
            // Note: Sections and Chunks don't have individual DeleteAsync methods
            // They only have DeleteByFileAsync which deletes all sections/chunks for a file

            // Assert - First store should see file deletion
            Assert.IsTrue(fileDeleted);

            Assert.IsFalse(await _knowledgeStore.Files.ExistsAsync(file.Id));
            Assert.IsNull(await _knowledgeStore.Files.GetAsync(file.Id));

            // Sections and chunks should still exist since we only deleted the file
            // (they would be deleted by foreign key cascade in a real scenario)
            KnowledgeFileSection? retrievedSection = await _knowledgeStore.Sections.GetAsync(section.Id);
            KnowledgeFileChunk? retrievedChunk = await _knowledgeStore.Chunks.GetAsync(chunk.Id);
            Assert.IsNotNull(retrievedSection);
            Assert.IsNotNull(retrievedChunk);
        }

        [TestMethod]
        public async Task DatabaseFile_ShouldBeCreated_WhenStoreIsInitialized()
        {
            // Arrange
            string dbPath = Path.GetTempFileName();
            File.Delete(dbPath); // Ensure file doesn't exist

            // Act
            SqliteKnowledgeStore store = new SqliteKnowledgeStore($"Data Source={dbPath}");
            await store.LoadAsync();

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
            await _knowledgeStore.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _knowledgeStore.Files.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Test summary",
                new float[] { 0.1f, 0.2f, 0.3f }
            );
            await _knowledgeStore.Sections.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Test content",
                new float[] { 0.4f, 0.5f, 0.6f }
            );
            await _knowledgeStore.Chunks.AddAsync(chunk);

            // Assert - File should exist and contain data
            Assert.IsTrue(File.Exists(_testDbPath));
            long fileSize = new FileInfo(_testDbPath).Length;
            Assert.IsTrue(fileSize > 0, "Database file should contain data");
        }

        [TestMethod]
        public async Task MultipleStores_ShouldHandleConcurrentWrites()
        {
            // Arrange
            await _knowledgeStore.LoadAsync();
            SqliteKnowledgeStore secondStore = new SqliteKnowledgeStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Act - Add data from both stores concurrently
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), $"concurrent{index}.txt", new byte[] { (byte)index });
                    await (index % 2 == 0 ? _knowledgeStore : secondStore).Files.AddAsync(file);

                    KnowledgeFileSection section = new KnowledgeFileSection(
                        Guid.NewGuid(),
                        file.Id,
                        0,
                        new List<KnowledgeFileChunk>(),
                        $"Section {index}",
                        new float[] { (float)index * 0.1f }
                    );
                    await (index % 2 == 0 ? _knowledgeStore : secondStore).Sections.AddAsync(section);

                    KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                        Guid.NewGuid(),
                        section.Id,
                        0,
                        $"Chunk {index}",
                        new float[] { (float)index * 0.2f }
                    );
                    await (index % 2 == 0 ? _knowledgeStore : secondStore).Chunks.AddAsync(chunk);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All data should be accessible from both stores
            IEnumerable<KnowledgeFile> allFiles = await _knowledgeStore.Files.GetAllAsync();

            Assert.AreEqual(5, allFiles.Count());

            // Verify second store sees the same data
            IEnumerable<KnowledgeFile> secondStoreFiles = await secondStore.Files.GetAllAsync();

            Assert.AreEqual(5, secondStoreFiles.Count());

            // Note: Sections and Chunks don't have GetAllAsync methods in the interface
            // We can verify they exist by checking that the files we created have associated data
        }

        [TestMethod]
        public async Task Store_ShouldHandleLargeDataSets()
        {
            // Arrange
            await _knowledgeStore.LoadAsync();
            const int dataCount = 100;

            // Act - Add many records
            List<KnowledgeFile> files = new List<KnowledgeFile>();
            List<KnowledgeFileSection> sections = new List<KnowledgeFileSection>();
            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();

            for (int i = 0; i < dataCount; i++)
            {
                KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), $"file{i}.txt", new byte[] { (byte)i });
                await _knowledgeStore.Files.AddAsync(file);
                files.Add(file);

                KnowledgeFileSection section = new KnowledgeFileSection(
                    Guid.NewGuid(),
                    file.Id,
                    i,
                    new List<KnowledgeFileChunk>(),
                    $"Summary {i}",
                    new float[] { (float)i * 0.1f, (float)i * 0.2f }
                );
                await _knowledgeStore.Sections.AddAsync(section);
                sections.Add(section);

                KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                    Guid.NewGuid(),
                    section.Id,
                    i,
                    $"Content {i}",
                    new float[] { (float)i * 0.3f, (float)i * 0.4f, (float)i * 0.5f }
                );
                await _knowledgeStore.Chunks.AddAsync(chunk);
                chunks.Add(chunk);
            }

            // Assert - All data should be retrievable
            IEnumerable<KnowledgeFile> allFiles = await _knowledgeStore.Files.GetAllAsync();

            Assert.AreEqual(dataCount, allFiles.Count());

            // Verify random samples
            Random random = new Random(42);
            for (int i = 0; i < 10; i++)
            {
                int index = random.Next(dataCount);
                Assert.IsTrue(await _knowledgeStore.Files.ExistsAsync(files[index].Id));

                // Verify sections and chunks exist by retrieving them
                KnowledgeFileSection? retrievedSection = await _knowledgeStore.Sections.GetAsync(sections[index].Id);
                KnowledgeFileChunk? retrievedChunk = await _knowledgeStore.Chunks.GetAsync(chunks[index].Id);
                Assert.IsNotNull(retrievedSection);
                Assert.IsNotNull(retrievedChunk);
            }
        }

        [TestMethod]
        public async Task Store_ShouldHandleDatabaseRecreation_WhenFileIsMissing()
        {
            // Arrange
            string missingDbPath = Path.GetTempFileName();
            File.Delete(missingDbPath); // Ensure file doesn't exist

            // Act & Assert - Should work with missing file
            SqliteKnowledgeStore store = new SqliteKnowledgeStore($"Data Source={missingDbPath}");
            await store.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "missing-file.txt", new byte[] { 1, 2, 3 });
            await store.Files.AddAsync(file);

            KnowledgeFileSection section = new KnowledgeFileSection(
                Guid.NewGuid(),
                file.Id,
                0,
                new List<KnowledgeFileChunk>(),
                "Missing section",
                new float[] { 0.1f, 0.2f }
            );
            await store.Sections.AddAsync(section);

            KnowledgeFileChunk chunk = new KnowledgeFileChunk(
                Guid.NewGuid(),
                section.Id,
                0,
                "Missing chunk",
                new float[] { 0.3f, 0.4f }
            );
            await store.Chunks.AddAsync(chunk);

            Assert.IsTrue(await store.Files.ExistsAsync(file.Id));

            // Verify sections and chunks exist by retrieving them
            KnowledgeFileSection? retrievedSection = await store.Sections.GetAsync(section.Id);
            KnowledgeFileChunk? retrievedChunk = await store.Chunks.GetAsync(chunk.Id);
            Assert.IsNotNull(retrievedSection);
            Assert.IsNotNull(retrievedChunk);

            // Cleanup
            try
            {
                File.Delete(missingDbPath);
            }
            catch { /* ignore cleanup errors */ }
        }
    }
}
