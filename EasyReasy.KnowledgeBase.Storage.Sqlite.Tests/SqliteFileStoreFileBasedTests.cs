using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite.Tests
{
    [TestClass]
    public sealed class SqliteFileStoreFileBasedTests
    {
        private string _testDbPath = string.Empty;
        private SqliteFileStore _fileStore = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Use a temporary file for testing persistence
            _testDbPath = Path.GetTempFileName();
            _fileStore = new SqliteFileStore($"Data Source={_testDbPath}");
        }

        [TestCleanup]
        public void TestCleanup()
        {
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
        public async Task Data_ShouldPersist_WhenStoreIsRecreated()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "persistent.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act - Create a new store instance with the same database file
            SqliteFileStore newStore = new SqliteFileStore($"Data Source={_testDbPath}");
            await newStore.LoadAsync();

            // Assert - Data should still be there
            KnowledgeFile? retrieved = await newStore.GetAsync(file.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(file.Id, retrieved.Id);
            Assert.AreEqual(file.Name, retrieved.Name);
            CollectionAssert.AreEqual(file.Hash, retrieved.Hash);
        }

        [TestMethod]
        public async Task MultipleStores_ShouldShareData_WhenUsingSameFile()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "shared.txt", new byte[] { 5, 6, 7, 8 });
            await _fileStore.AddAsync(file);

            // Act - Create a second store instance
            SqliteFileStore secondStore = new SqliteFileStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Assert - Both stores should see the same data
            Assert.IsTrue(await _fileStore.ExistsAsync(file.Id));
            Assert.IsTrue(await secondStore.ExistsAsync(file.Id));

            KnowledgeFile? fromFirst = await _fileStore.GetAsync(file.Id);
            KnowledgeFile? fromSecond = await secondStore.GetAsync(file.Id);

            Assert.IsNotNull(fromFirst);
            Assert.IsNotNull(fromSecond);
            Assert.AreEqual(fromFirst.Id, fromSecond.Id);
            Assert.AreEqual(fromFirst.Name, fromSecond.Name);
            CollectionAssert.AreEqual(fromFirst.Hash, fromSecond.Hash);
        }

        [TestMethod]
        public async Task Updates_ShouldBeVisible_AcrossStoreInstances()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "original.txt", new byte[] { 1, 2, 3 });
            await _fileStore.AddAsync(file);

            SqliteFileStore secondStore = new SqliteFileStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Act - Update from second store
            KnowledgeFile updatedFile = new KnowledgeFile(file.Id, "updated.txt", new byte[] { 4, 5, 6 });
            await secondStore.UpdateAsync(updatedFile);

            // Assert - First store should see the update
            KnowledgeFile? retrieved = await _fileStore.GetAsync(file.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("updated.txt", retrieved.Name);
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, retrieved.Hash);
        }

        [TestMethod]
        public async Task Deletes_ShouldBeVisible_AcrossStoreInstances()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "to-delete.txt", new byte[] { 1, 2, 3 });
            await _fileStore.AddAsync(file);

            SqliteFileStore secondStore = new SqliteFileStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Act - Delete from second store
            bool deleted = await secondStore.DeleteAsync(file.Id);

            // Assert - First store should see the deletion
            Assert.IsTrue(deleted);
            Assert.IsFalse(await _fileStore.ExistsAsync(file.Id));
            Assert.IsNull(await _fileStore.GetAsync(file.Id));
        }

        [TestMethod]
        public async Task DatabaseFile_ShouldBeCreated_WhenStoreIsInitialized()
        {
            // Arrange
            string dbPath = Path.GetTempFileName();
            File.Delete(dbPath); // Ensure file doesn't exist

            // Act
            SqliteFileStore store = new SqliteFileStore($"Data Source={dbPath}");
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
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });

            // Act - Add data
            await _fileStore.AddAsync(file);

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
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });

            // Act - Perform various operations
            await _fileStore.AddAsync(file);
            await _fileStore.UpdateAsync(new KnowledgeFile(file.Id, "updated.txt", new byte[] { 5, 6, 7, 8 }));
            await _fileStore.DeleteAsync(file.Id);

            // Assert - Database should still be valid and accessible
            Assert.IsTrue(File.Exists(_testDbPath));

            // Create new store to verify database integrity
            SqliteFileStore newStore = new SqliteFileStore($"Data Source={_testDbPath}");
            await newStore.LoadAsync();

            // Should be able to add new data
            KnowledgeFile newFile = new KnowledgeFile(Guid.NewGuid(), "new.txt", new byte[] { 9, 10, 11 });
            await newStore.AddAsync(newFile);
            Assert.IsTrue(await newStore.ExistsAsync(newFile.Id));
        }

        [TestMethod]
        public async Task MultipleStores_ShouldHandleConcurrentWrites()
        {
            // Arrange
            await _fileStore.LoadAsync();
            SqliteFileStore secondStore = new SqliteFileStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Act - Add files from both stores concurrently
            List<Task<Guid>> tasks = new List<Task<Guid>>();
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), $"concurrent{index}.txt", new byte[] { (byte)index });
                    return await (index % 2 == 0 ? _fileStore : secondStore).AddAsync(file);
                }));
            }

            Guid[] results = await Task.WhenAll(tasks);

            // Assert - All files should be added successfully
            Assert.AreEqual(5, results.Length);
            foreach (Guid id in results)
            {
                Assert.IsTrue(await _fileStore.ExistsAsync(id));
                Assert.IsTrue(await secondStore.ExistsAsync(id));
            }
        }

        [TestMethod]
        public async Task MultipleStores_ShouldHandleConcurrentReads()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "concurrent-read.txt", new byte[] { 1, 2, 3 });
            await _fileStore.AddAsync(file);

            SqliteFileStore secondStore = new SqliteFileStore($"Data Source={_testDbPath}");
            await secondStore.LoadAsync();

            // Act - Read from both stores concurrently
            List<Task<KnowledgeFile?>> tasks = new List<Task<KnowledgeFile?>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () => await (i % 2 == 0 ? _fileStore : secondStore).GetAsync(file.Id)));
            }

            KnowledgeFile?[] results = await Task.WhenAll(tasks);

            // Assert - All reads should return the same data
            Assert.AreEqual(10, results.Length);
            foreach (KnowledgeFile? result in results)
            {
                Assert.IsNotNull(result);
                Assert.AreEqual(file.Id, result.Id);
                Assert.AreEqual(file.Name, result.Name);
                CollectionAssert.AreEqual(file.Hash, result.Hash);
            }
        }

        [TestMethod]
        public async Task Store_ShouldHandleLargeFiles()
        {
            // Arrange
            await _fileStore.LoadAsync();
            byte[] largeHash = new byte[1024 * 1024]; // 1MB hash
            new Random(42).NextBytes(largeHash); // Fill with random data
            KnowledgeFile largeFile = new KnowledgeFile(Guid.NewGuid(), "large-file.txt", largeHash);

            // Act
            Guid result = await _fileStore.AddAsync(largeFile);

            // Assert
            Assert.AreEqual(largeFile.Id, result);
            KnowledgeFile? retrieved = await _fileStore.GetAsync(largeFile.Id);
            Assert.IsNotNull(retrieved);
            CollectionAssert.AreEqual(largeHash, retrieved.Hash);
        }

        [TestMethod]
        public async Task Store_ShouldHandleManyFiles()
        {
            // Arrange
            await _fileStore.LoadAsync();
            const int fileCount = 1000;

            // Act - Add many files
            List<KnowledgeFile> files = new List<KnowledgeFile>();
            for (int i = 0; i < fileCount; i++)
            {
                KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), $"file{i}.txt", new byte[] { (byte)i });
                await _fileStore.AddAsync(file);
                files.Add(file);
            }

            // Assert - All files should be retrievable
            foreach (KnowledgeFile file in files)
            {
                Assert.IsTrue(await _fileStore.ExistsAsync(file.Id));
                KnowledgeFile? retrieved = await _fileStore.GetAsync(file.Id);
                Assert.IsNotNull(retrieved);
                Assert.AreEqual(file.Name, retrieved.Name);
            }

            // Verify total count
            IEnumerable<KnowledgeFile> allFiles = await _fileStore.GetAllAsync();
            Assert.AreEqual(fileCount, allFiles.Count());
        }

        [TestMethod]
        public async Task Store_ShouldHandleDatabaseRecreation_WhenFileIsMissing()
        {
            // Arrange
            string missingDbPath = Path.GetTempFileName();
            File.Delete(missingDbPath); // Ensure file doesn't exist

            // Act & Assert - Should work with missing file
            SqliteFileStore store = new SqliteFileStore($"Data Source={missingDbPath}");
            await store.LoadAsync();

            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "missing-file.txt", new byte[] { 1, 2, 3 });
            await store.AddAsync(file);
            Assert.IsTrue(await store.ExistsAsync(file.Id));

            // Cleanup
            try
            {
                File.Delete(missingDbPath);
            }
            catch { /* ignore cleanup errors */ }
        }
    }
}
