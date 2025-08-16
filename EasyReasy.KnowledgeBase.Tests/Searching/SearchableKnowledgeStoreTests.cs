using EasyReasy.KnowledgeBase.Models;
using EasyReasy.KnowledgeBase.Searching;
using EasyReasy.KnowledgeBase.Searchings;
using EasyReasy.KnowledgeBase.Storage;

namespace EasyReasy.KnowledgeBase.Tests.Searching
{
    [TestClass]
    public sealed class SearchableKnowledgeStoreTests
    {
        private MockFileStore _fileStore = null!;
        private MockSectionStore _sectionStore = null!;
        private MockChunkStore _chunkStore = null!;
        private MockKnowledgeVectorStore _chunksVectorStore = null!;
        private MockKnowledgeVectorStore _sectionsVectorStore = null!;
        private SearchableKnowledgeStore _searchableStore = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _fileStore = new MockFileStore();
            _sectionStore = new MockSectionStore();
            _chunkStore = new MockChunkStore();
            _chunksVectorStore = new MockKnowledgeVectorStore();
            _sectionsVectorStore = new MockKnowledgeVectorStore();

            _searchableStore = new SearchableKnowledgeStore(
                _fileStore,
                _sectionStore,
                _chunkStore,
                _chunksVectorStore,
                _sectionsVectorStore);
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenFileStoreIsNull()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new SearchableKnowledgeStore(
                fileStore: null!,
                sectionStore: _sectionStore,
                chunkStore: _chunkStore,
                chunksVectorStore: _chunksVectorStore,
                sectionsVectorStore: _sectionsVectorStore));
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenSectionStoreIsNull()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new SearchableKnowledgeStore(
                fileStore: _fileStore,
                sectionStore: null!,
                chunkStore: _chunkStore,
                chunksVectorStore: _chunksVectorStore,
                sectionsVectorStore: _sectionsVectorStore));
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenChunkStoreIsNull()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new SearchableKnowledgeStore(
                fileStore: _fileStore,
                sectionStore: _sectionStore,
                chunkStore: null!,
                chunksVectorStore: _chunksVectorStore,
                sectionsVectorStore: _sectionsVectorStore));
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenChunksVectorStoreIsNull()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new SearchableKnowledgeStore(
                fileStore: _fileStore,
                sectionStore: _sectionStore,
                chunkStore: _chunkStore,
                chunksVectorStore: null!,
                sectionsVectorStore: _sectionsVectorStore));
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenSectionsVectorStoreIsNull()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new SearchableKnowledgeStore(
                fileStore: _fileStore,
                sectionStore: _sectionStore,
                chunkStore: _chunkStore,
                chunksVectorStore: _chunksVectorStore,
                sectionsVectorStore: null!));
        }

        [TestMethod]
        public void Constructor_ShouldAccept_WhenAllParametersAreValid()
        {
            // Act & Assert
            SearchableKnowledgeStore store = new SearchableKnowledgeStore(
                _fileStore,
                _sectionStore,
                _chunkStore,
                _chunksVectorStore,
                _sectionsVectorStore);

            Assert.IsNotNull(store);
            Assert.IsNotNull(store.Files);
            Assert.IsNotNull(store.Sections);
            Assert.IsNotNull(store.Chunks);
        }

        [TestMethod]
        public void Files_ShouldReturnInjectedFileStore()
        {
            // Act
            IFileStore result = _searchableStore.Files;

            // Assert
            Assert.AreSame(_fileStore, result);
        }

        [TestMethod]
        public void Sections_ShouldReturnInjectedSectionStore()
        {
            // Act
            ISectionStore result = _searchableStore.Sections;

            // Assert
            Assert.AreSame(_sectionStore, result);
        }

        [TestMethod]
        public void Chunks_ShouldReturnInjectedChunkStore()
        {
            // Act
            IChunkStore result = _searchableStore.Chunks;

            // Assert
            Assert.AreSame(_chunkStore, result);
        }

        [TestMethod]
        public void GetChunksVectorStore_ShouldReturnInjectedChunksVectorStore()
        {
            // Act
            IKnowledgeVectorStore result = _searchableStore.GetChunksVectorStore();

            // Assert
            Assert.AreSame(_chunksVectorStore, result);
        }

        [TestMethod]
        public void GetSectionsVectorStore_ShouldReturnInjectedSectionsVectorStore()
        {
            // Act
            IKnowledgeVectorStore result = _searchableStore.GetSectionsVectorStore();

            // Assert
            Assert.AreSame(_sectionsVectorStore, result);
        }

        #region Mock Classes

        private sealed class MockFileStore : IFileStore
        {
            public Task<Guid> AddAsync(KnowledgeFile file) => Task.FromResult(file.Id);
            public Task<bool> DeleteAsync(Guid fileId) => Task.FromResult(true);
            public Task<bool> ExistsAsync(Guid fileId) => Task.FromResult(false);
            public Task<KnowledgeFile?> GetAsync(Guid fileId) => Task.FromResult<KnowledgeFile?>(null);
            public Task<IEnumerable<KnowledgeFile>> GetAllAsync() => Task.FromResult<IEnumerable<KnowledgeFile>>(Array.Empty<KnowledgeFile>());
            public Task UpdateAsync(KnowledgeFile file) => Task.CompletedTask;
        }

        private sealed class MockSectionStore : ISectionStore
        {
            public Task AddAsync(KnowledgeFileSection section) => Task.CompletedTask;
            public Task<bool> DeleteByFileAsync(Guid fileId) => Task.FromResult(true);
            public Task<KnowledgeFileSection?> GetAsync(Guid sectionId) => Task.FromResult<KnowledgeFileSection?>(null);
            public Task<KnowledgeFileSection?> GetByIndexAsync(Guid fileId, int sectionIndex) => Task.FromResult<KnowledgeFileSection?>(null);
        }

        private sealed class MockChunkStore : IChunkStore
        {
            public Task AddAsync(KnowledgeFileChunk chunk) => Task.CompletedTask;
            public Task<bool> DeleteByFileAsync(Guid fileId) => Task.FromResult(true);
            public Task<KnowledgeFileChunk?> GetAsync(Guid chunkId) => Task.FromResult<KnowledgeFileChunk?>(null);
            public Task<KnowledgeFileChunk?> GetByIndexAsync(Guid sectionId, int chunkIndex) => Task.FromResult<KnowledgeFileChunk?>(null);
        }

        private sealed class MockKnowledgeVectorStore : IKnowledgeVectorStore
        {
            public Task AddAsync(Guid guid, float[] vector) => Task.CompletedTask;
            public Task RemoveAsync(Guid guid) => Task.CompletedTask;
            public Task<IEnumerable<IKnowledgeVector>> SearchAsync(float[] queryVector, int maxResultsCount) => Task.FromResult<IEnumerable<IKnowledgeVector>>(Array.Empty<IKnowledgeVector>());
        }

        #endregion
    }
} 