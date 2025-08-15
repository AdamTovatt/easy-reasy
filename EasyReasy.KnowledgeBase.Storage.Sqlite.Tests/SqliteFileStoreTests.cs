using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite.Tests
{
    [TestClass]
    public sealed class SqliteFileStoreTests
    {
        private string _testDbPath = string.Empty;
        private SqliteFileStore _fileStore = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Use a temporary file for testing
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
        public void Constructor_ShouldThrow_WhenConnectionStringIsNull()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new SqliteFileStore(null!));
        }

        [TestMethod]
        public void Constructor_ShouldAccept_WhenConnectionStringIsValid()
        {
            // Act & Assert
            SqliteFileStore store = new SqliteFileStore("Data Source=:memory:");
            Assert.IsNotNull(store);
        }

        [TestMethod]
        public async Task AddAsync_ShouldAddFile_WhenValidFileProvided()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });

            // Act
            Guid result = await _fileStore.AddAsync(file);

            // Assert
            Assert.AreEqual(file.Id, result);
            KnowledgeFile? retrieved = await _fileStore.GetAsync(file.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(file.Id, retrieved.Id);
            Assert.AreEqual(file.Name, retrieved.Name);
            CollectionAssert.AreEqual(file.Hash, retrieved.Hash);
        }

        [TestMethod]
        public async Task AddAsync_ShouldThrow_WhenFileIsNull()
        {
            // Arrange
            await _fileStore.LoadAsync();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _fileStore.AddAsync(null!));
        }

        [TestMethod]
        public async Task AddAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });

            // Act
            Guid result = await _fileStore.AddAsync(file);

            // Assert
            Assert.AreEqual(file.Id, result);
            KnowledgeFile? retrieved = await _fileStore.GetAsync(file.Id);
            Assert.IsNotNull(retrieved);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnFile_WhenFileExists()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act
            KnowledgeFile? result = await _fileStore.GetAsync(file.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(file.Id, result.Id);
            Assert.AreEqual(file.Name, result.Name);
            CollectionAssert.AreEqual(file.Hash, result.Hash);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnNull_WhenFileDoesNotExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            Guid nonExistentId = Guid.NewGuid();

            // Act
            KnowledgeFile? result = await _fileStore.GetAsync(nonExistentId);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act
            KnowledgeFile? result = await _fileStore.GetAsync(file.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(file.Id, result.Id);
        }

        [TestMethod]
        public async Task ExistsAsync_ShouldReturnTrue_WhenFileExists()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act
            bool result = await _fileStore.ExistsAsync(file.Id);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ExistsAsync_ShouldReturnFalse_WhenFileDoesNotExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            Guid nonExistentId = Guid.NewGuid();

            // Act
            bool result = await _fileStore.ExistsAsync(nonExistentId);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ExistsAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act
            bool result = await _fileStore.ExistsAsync(file.Id);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task GetAllAsync_ShouldReturnEmptyCollection_WhenNoFilesExist()
        {
            // Arrange
            await _fileStore.LoadAsync();

            // Act
            IEnumerable<KnowledgeFile> result = await _fileStore.GetAllAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public async Task GetAllAsync_ShouldReturnAllFiles_WhenFilesExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file1 = new KnowledgeFile(Guid.NewGuid(), "test1.txt", new byte[] { 1, 2, 3 });
            KnowledgeFile file2 = new KnowledgeFile(Guid.NewGuid(), "test2.txt", new byte[] { 4, 5, 6 });
            await _fileStore.AddAsync(file1);
            await _fileStore.AddAsync(file2);

            // Act
            IEnumerable<KnowledgeFile> result = await _fileStore.GetAllAsync();

            // Assert
            Assert.IsNotNull(result);
            List<KnowledgeFile> files = result.ToList();
            Assert.AreEqual(2, files.Count);
            Assert.IsTrue(files.Any(f => f.Id == file1.Id));
            Assert.IsTrue(files.Any(f => f.Id == file2.Id));
        }

        [TestMethod]
        public async Task GetAllAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act
            IEnumerable<KnowledgeFile> result = await _fileStore.GetAllAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());
        }

        [TestMethod]
        public async Task UpdateAsync_ShouldUpdateFile_WhenFileExists()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFile updatedFile = new KnowledgeFile(file.Id, "updated.txt", new byte[] { 5, 6, 7, 8 });

            // Act
            await _fileStore.UpdateAsync(updatedFile);

            // Assert
            KnowledgeFile? result = await _fileStore.GetAsync(file.Id);
            Assert.IsNotNull(result);
            Assert.AreEqual(updatedFile.Name, result.Name);
            CollectionAssert.AreEqual(updatedFile.Hash, result.Hash);
        }

        [TestMethod]
        public async Task UpdateAsync_ShouldThrow_WhenFileIsNull()
        {
            // Arrange
            await _fileStore.LoadAsync();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => _fileStore.UpdateAsync(null!));
        }

        [TestMethod]
        public async Task UpdateAsync_ShouldThrow_WhenFileDoesNotExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile nonExistentFile = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _fileStore.UpdateAsync(nonExistentFile));
        }

        [TestMethod]
        public async Task UpdateAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            KnowledgeFile updatedFile = new KnowledgeFile(file.Id, "updated.txt", new byte[] { 5, 6, 7, 8 });

            // Act
            await _fileStore.UpdateAsync(updatedFile);

            // Assert
            KnowledgeFile? result = await _fileStore.GetAsync(file.Id);
            Assert.IsNotNull(result);
            Assert.AreEqual(updatedFile.Name, result.Name);
        }

        [TestMethod]
        public async Task DeleteAsync_ShouldReturnTrue_WhenFileExists()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act
            bool result = await _fileStore.DeleteAsync(file.Id);

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(await _fileStore.ExistsAsync(file.Id));
        }

        [TestMethod]
        public async Task DeleteAsync_ShouldReturnFalse_WhenFileDoesNotExist()
        {
            // Arrange
            await _fileStore.LoadAsync();
            Guid nonExistentId = Guid.NewGuid();

            // Act
            bool result = await _fileStore.DeleteAsync(nonExistentId);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task DeleteAsync_ShouldInitializeDatabase_WhenNotInitialized()
        {
            // Arrange
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);

            // Act
            bool result = await _fileStore.DeleteAsync(file.Id);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task LoadAsync_ShouldInitializeDatabase_WhenCalled()
        {
            // Act
            await _fileStore.LoadAsync();

            // Assert
            // Should not throw and database should be accessible
            KnowledgeFile file = new KnowledgeFile(Guid.NewGuid(), "test.txt", new byte[] { 1, 2, 3, 4 });
            await _fileStore.AddAsync(file);
            Assert.IsTrue(await _fileStore.ExistsAsync(file.Id));
        }

        [TestMethod]
        public async Task LoadAsync_ShouldNotReinitialize_WhenAlreadyInitialized()
        {
            // Arrange
            await _fileStore.LoadAsync();

            // Act & Assert
            // Should not throw on second call
            await _fileStore.LoadAsync();
        }

        [TestMethod]
        public async Task SaveAsync_ShouldComplete_WhenCalled()
        {
            // Arrange
            await _fileStore.LoadAsync();

            // Act & Assert
            // Should not throw
            await _fileStore.SaveAsync();
        }

        [TestMethod]
        public async Task FullWorkflow_ShouldWorkCorrectly()
        {
            // Arrange
            await _fileStore.LoadAsync();
            KnowledgeFile file1 = new KnowledgeFile(Guid.NewGuid(), "workflow1.txt", new byte[] { 1, 2, 3 });
            KnowledgeFile file2 = new KnowledgeFile(Guid.NewGuid(), "workflow2.txt", new byte[] { 4, 5, 6 });

            // Act - Add files
            Guid id1 = await _fileStore.AddAsync(file1);
            Guid id2 = await _fileStore.AddAsync(file2);

            // Assert - Files should exist
            Assert.IsTrue(await _fileStore.ExistsAsync(id1));
            Assert.IsTrue(await _fileStore.ExistsAsync(id2));

            // Act - Get all files
            IEnumerable<KnowledgeFile> allFiles = await _fileStore.GetAllAsync();

            // Assert - Should have 2 files
            Assert.AreEqual(2, allFiles.Count());

            // Act - Update file
            KnowledgeFile updatedFile = new KnowledgeFile(id1, "updated-workflow1.txt", new byte[] { 7, 8, 9 });
            await _fileStore.UpdateAsync(updatedFile);

            // Assert - File should be updated
            KnowledgeFile? retrieved = await _fileStore.GetAsync(id1);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("updated-workflow1.txt", retrieved.Name);

            // Act - Delete one file
            bool deleted = await _fileStore.DeleteAsync(id1);

            // Assert - File should be deleted
            Assert.IsTrue(deleted);
            Assert.IsFalse(await _fileStore.ExistsAsync(id1));
            Assert.IsTrue(await _fileStore.ExistsAsync(id2));

            // Act - Get all files again
            allFiles = await _fileStore.GetAllAsync();

            // Assert - Should have 1 file
            Assert.AreEqual(1, allFiles.Count());
        }
    }
}
