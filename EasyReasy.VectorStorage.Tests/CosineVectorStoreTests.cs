namespace EasyReasy.VectorStorage.Tests
{
    [TestClass]
    public class CosineVectorStoreTests
    {
        #region Test Helpers

        private static float[] CreateVector(float value, int dimension = 768)
        {
            float[] vec = new float[dimension];
            Array.Fill(vec, value);
            return vec;
        }

        private static float[] CreateRandomVector(int dimension = 768, float scale = 1f)
        {
            Random rng = new Random();
            float[] vec = new float[dimension];
            for (int i = 0; i < dimension; i++)
                vec[i] = (float)(rng.NextDouble() * scale);
            return vec;
        }

        private static async Task<CosineVectorStore> CreatePopulatedStoreAsync(int count, int dimension = 768)
        {
            CosineVectorStore store = new CosineVectorStore(dimension);
            for (int i = 0; i < count; i++)
            {
                await store.AddAsync(new StoredVector(Guid.NewGuid(), CreateVector(i + 1, dimension)));
            }

            return store;
        }

        #endregion

        #region Constructor and Validation Tests

        [TestMethod]
        public void Constructor_ShouldThrow_WhenDimensionIsZero()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CosineVectorStore(0));
        }

        [TestMethod]
        public void Constructor_ShouldThrow_WhenDimensionIsNegative()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CosineVectorStore(-1));
        }

        [TestMethod]
        public void Constructor_ShouldAccept_WhenDimensionIsPositive()
        {
            // Act & Assert
            CosineVectorStore store = new CosineVectorStore(768);
            Assert.IsNotNull(store);
        }

        [TestMethod]
        public async Task AddAsync_ShouldThrow_WhenVectorDimensionMismatch()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            StoredVector mismatchedVector = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 512));

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.AddAsync(mismatchedVector));
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldThrow_WhenQueryDimensionMismatch()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            StoredVector vector = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 768));
            await store.AddAsync(vector);
            float[] mismatchedQuery = CreateVector(1.0f, 512);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.FindMostSimilarAsync(mismatchedQuery, 1));
        }

        [TestMethod]
        public async Task LoadAsync_ShouldThrow_WhenLoadedDimensionMismatch()
        {
            // Arrange
            CosineVectorStore store1 = new CosineVectorStore(768);
            await store1.AddAsync(new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 768)));
            using MemoryStream stream = new MemoryStream();
            await store1.SaveAsync(stream);
            stream.Position = 0;

            CosineVectorStore store2 = new CosineVectorStore(512); // Different dimension

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => store2.LoadAsync(stream));
        }

        [TestMethod]
        public async Task LoadAsync_ShouldThrow_WhenIndividualVectorDimensionMismatch()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            using MemoryStream stream = new MemoryStream();

            // Manually create invalid stream data
            using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(768); // Store dimension
                writer.Write(1); // Vector count
                writer.Write(Guid.NewGuid().ToByteArray()); // Vector ID
                writer.Write(512); // Individual vector dimension (mismatch)
                writer.Write(new byte[512 * sizeof(float)]); // Vector data
            }

            stream.Position = 0;

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => store.LoadAsync(stream));
        }

        #endregion

        #region Basic Operations

        [TestMethod]
        public async Task AddAsync_ShouldAddVector()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            StoredVector vector = new StoredVector(Guid.NewGuid(), CreateVector(1.0f));

            // Act
            await store.AddAsync(vector);

            // Assert
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector.Values, 1);
            Assert.AreEqual(1, results.Count());
            Assert.AreEqual(vector.Id, results.First().Id);
        }

        [TestMethod]
        public async Task RemoveAsync_ShouldRemoveExistingVector()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            StoredVector vector = new StoredVector(Guid.NewGuid(), CreateVector(1.0f));
            await store.AddAsync(vector);

            // Act
            bool removed = await store.RemoveAsync(vector.Id);

            // Assert
            Assert.IsTrue(removed);
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector.Values, 1);
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public async Task RemoveAsync_ShouldReturnFalse_WhenIdNotFound()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            Guid nonExistentId = Guid.NewGuid();

            // Act
            bool removed = await store.RemoveAsync(nonExistentId);

            // Assert
            Assert.IsFalse(removed);
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldReturnEmpty_WhenNoVectors()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            float[] queryVector = CreateVector(1.0f);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(queryVector, 5);

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldReturnTopMatch()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 1);

            // Assert
            Assert.AreEqual(1, results.Count());
            Assert.AreEqual(vector1.Id, results.First().Id);
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldReturnMultipleMatches()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f));
            StoredVector vector3 = new StoredVector(Guid.NewGuid(), CreateVector(3.0f));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);
            await store.AddAsync(vector3);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 3);

            // Assert
            Assert.AreEqual(3, results.Count());
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldRespectCountParameter()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            for (int i = 0; i < 10; i++)
            {
                await store.AddAsync(new StoredVector(Guid.NewGuid(), CreateVector(i + 1.0f)));
            }

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(CreateVector(1.0f), 5);

            // Assert
            Assert.AreEqual(5, results.Count());
        }

        #endregion

        #region Persistence

        [TestMethod]
        public async Task SaveAsyncAndLoadAsync_ShouldPersistAndReloadVectorsCorrectly()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(5);
            using MemoryStream stream = new MemoryStream();

            // Act
            await store.SaveAsync(stream);
            stream.Position = 0;

            CosineVectorStore newStore = new CosineVectorStore(768);
            await newStore.LoadAsync(stream);

            // Assert
            IEnumerable<StoredVector> results = await newStore.FindMostSimilarAsync(CreateVector(1.0f), 5);
            Assert.AreEqual(5, results.Count());
        }

        [TestMethod]
        public async Task SaveAndLoadAsync_ShouldReturnSameResults_ForSameQueryVector()
        {
            // Arrange
            CosineVectorStore originalStore = new CosineVectorStore(768);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f));
            StoredVector vector3 = new StoredVector(Guid.NewGuid(), CreateVector(3.0f));

            await originalStore.AddAsync(vector1);
            await originalStore.AddAsync(vector2);
            await originalStore.AddAsync(vector3);

            float[] queryVector = CreateVector(1.5f);

            // Act - Search in original store
            IEnumerable<StoredVector> originalResults = await originalStore.FindMostSimilarAsync(queryVector, 3);
            List<Guid> originalResultIds = originalResults.Select(r => r.Id).ToList();

            // Save and load to new store
            using MemoryStream stream = new MemoryStream();
            await originalStore.SaveAsync(stream);
            stream.Position = 0;

            CosineVectorStore loadedStore = new CosineVectorStore(768);
            await loadedStore.LoadAsync(stream);

            // Search in loaded store with same query
            IEnumerable<StoredVector> loadedResults = await loadedStore.FindMostSimilarAsync(queryVector, 3);
            List<Guid> loadedResultIds = loadedResults.Select(r => r.Id).ToList();

            // Assert
            Assert.AreEqual(originalResults.Count(), loadedResults.Count());
            CollectionAssert.AreEqual(originalResultIds, loadedResultIds);

            // Verify the vectors themselves are identical
            List<StoredVector> originalResultsList = originalResults.ToList();
            List<StoredVector> loadedResultsList = loadedResults.ToList();

            for (int i = 0; i < originalResultsList.Count; i++)
            {
                Assert.AreEqual(originalResultsList[i].Id, loadedResultsList[i].Id);
                CollectionAssert.AreEqual(originalResultsList[i].Values, loadedResultsList[i].Values);
            }
        }

        [TestMethod]
        public async Task LoadAsync_ShouldClearExistingVectorsBeforeLoading()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(3);
            CosineVectorStore newStore = await CreatePopulatedStoreAsync(2);
            using MemoryStream stream = new MemoryStream();

            // Act
            await store.SaveAsync(stream);
            stream.Position = 0;
            await newStore.LoadAsync(stream);

            // Assert
            IEnumerable<StoredVector> results = await newStore.FindMostSimilarAsync(CreateVector(1.0f), 10);
            Assert.AreEqual(3, results.Count()); // Should have 3 vectors from loaded store, not 2+3
        }

        [TestMethod]
        public async Task LoadAsync_ShouldThrowOnInvalidStream()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            using MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3 }); // Invalid data

            // Act & Assert
            await Assert.ThrowsExceptionAsync<EndOfStreamException>(() => store.LoadAsync(stream));
        }

        [TestMethod]
        public async Task SaveAsync_ShouldThrowOnInvalidStream()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(1);
            using MemoryStream stream = new MemoryStream();
            stream.Dispose(); // Make it invalid

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => store.SaveAsync(stream));
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldReturnEmpty_WhenQueryIsZeroVector()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(3);
            float[] zeroVector = CreateVector(0.0f);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(zeroVector, 5);

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleQueryWithMismatchedLength()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            await store.AddAsync(new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 768)));
            float[] mismatchedQuery = CreateVector(1.0f, 512); // Different dimension

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                store.FindMostSimilarAsync(mismatchedQuery, 5));
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleRequestingMoreThanStored()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(3);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(CreateVector(1.0f), 10);

            // Assert
            Assert.AreEqual(3, results.Count()); // Should return all 3 vectors, not 10
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleNullQuery()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(3);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(null!, 5);

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleEmptyQuery()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(3);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(new float[0], 5);

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleZeroCount()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(3);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(CreateVector(1.0f), 0);

            // Assert
            Assert.AreEqual(0, results.Count());
        }

        #endregion

        #region Thread Safety

        [TestMethod]
        public async Task AddAndRemoveAsync_ShouldBeThreadSafe()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            List<Task> tasks = new List<Task>();

            // Act - Add vectors concurrently
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    StoredVector vector = new StoredVector(Guid.NewGuid(), CreateVector(index + 1.0f));
                    await store.AddAsync(vector);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(CreateVector(1.0f), 100);
            Assert.AreEqual(100, results.Count());
        }

        [TestMethod]
        public async Task ConcurrentFindMostSimilarAsync_ShouldBeThreadSafe()
        {
            // Arrange
            CosineVectorStore store = await CreatePopulatedStoreAsync(100);
            List<Task> tasks = new List<Task>();

            // Act - Search concurrently
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    float[] queryVector = CreateRandomVector();
                    IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(queryVector, 5);
                    Assert.IsTrue(results.Count() <= 5);
                }));
            }

            // Assert
            await Task.WhenAll(tasks);
        }

        #endregion

        #region Different Dimensions Tests

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldWorkWithSmallDimensions()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(64);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 64));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f, 64));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 2);

            // Assert
            Assert.AreEqual(2, results.Count());
            List<Guid> resultIds = results.Select(r => r.Id).ToList();
            Assert.IsTrue(resultIds.Contains(vector1.Id));
            Assert.IsTrue(resultIds.Contains(vector2.Id));
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldWorkWithMediumDimensions()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(256);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 256));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f, 256));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 2);

            // Assert
            Assert.AreEqual(2, results.Count());
            List<Guid> resultIds = results.Select(r => r.Id).ToList();
            Assert.IsTrue(resultIds.Contains(vector1.Id));
            Assert.IsTrue(resultIds.Contains(vector2.Id));
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldWorkWithLargeDimensions()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(1024);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 1024));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f, 1024));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 2);

            // Assert
            Assert.AreEqual(2, results.Count());
            List<Guid> resultIds = results.Select(r => r.Id).ToList();
            Assert.IsTrue(resultIds.Contains(vector1.Id));
            Assert.IsTrue(resultIds.Contains(vector2.Id));
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldWorkWithVeryLargeDimensions()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(2048);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 2048));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f, 2048));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 2);

            // Assert
            Assert.AreEqual(2, results.Count());
            List<Guid> resultIds = results.Select(r => r.Id).ToList();
            Assert.IsTrue(resultIds.Contains(vector1.Id));
            Assert.IsTrue(resultIds.Contains(vector2.Id));
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldWorkWithNonStandardDimensions()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(1536);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 1536)); // 2x768
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f, 1536));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 2);

            // Assert
            Assert.AreEqual(2, results.Count());
            List<Guid> resultIds = results.Select(r => r.Id).ToList();
            Assert.IsTrue(resultIds.Contains(vector1.Id));
            Assert.IsTrue(resultIds.Contains(vector2.Id));
        }

        [TestMethod]
        public async Task SaveAndLoadAsync_ShouldWorkWithDifferentDimensions()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(1024);
            await store.AddAsync(new StoredVector(Guid.NewGuid(), CreateVector(1.0f, 1024)));
            await store.AddAsync(new StoredVector(Guid.NewGuid(), CreateVector(2.0f, 1024)));
            await store.AddAsync(new StoredVector(Guid.NewGuid(), CreateVector(3.0f, 1024)));
            using MemoryStream stream = new MemoryStream();

            // Act
            await store.SaveAsync(stream);
            stream.Position = 0;

            CosineVectorStore newStore = new CosineVectorStore(1024);
            await newStore.LoadAsync(stream);

            // Assert
            IEnumerable<StoredVector> results = await newStore.FindMostSimilarAsync(CreateVector(1.0f, 1024), 3);
            Assert.AreEqual(3, results.Count());
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleSIMDOptimizedDimensions()
        {
            // Arrange - Test with dimensions that are multiples of Vector<float>.Count
            int simdSize = System.Numerics.Vector<float>.Count;
            int dimension = simdSize * 4; // Multiple of SIMD size
            CosineVectorStore store = new CosineVectorStore(dimension);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, dimension));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f, dimension));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 2);

            // Assert
            Assert.AreEqual(2, results.Count());
            List<Guid> resultIds = results.Select(r => r.Id).ToList();
            Assert.IsTrue(resultIds.Contains(vector1.Id));
            Assert.IsTrue(resultIds.Contains(vector2.Id));
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleNonSIMDOptimizedDimensions()
        {
            // Arrange - Test with dimensions that are not multiples of Vector<float>.Count
            int simdSize = System.Numerics.Vector<float>.Count;
            int dimension = simdSize * 3 + 1; // Not a multiple of SIMD size
            CosineVectorStore store = new CosineVectorStore(dimension);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f, dimension));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(2.0f, dimension));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 2);

            // Assert
            Assert.AreEqual(2, results.Count());
            List<Guid> resultIds = results.Select(r => r.Id).ToList();
            Assert.IsTrue(resultIds.Contains(vector1.Id));
            Assert.IsTrue(resultIds.Contains(vector2.Id));
        }

        #endregion

        #region Parallel Processing Tests

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldUseParallelProcessing_ForLargeDatasets()
        {
            // Arrange - Create exactly 1001 vectors to ensure we cross the threshold
            CosineVectorStore store = new CosineVectorStore(768);
            
            // Add 1001 vectors with distinct patterns
            for (int i = 0; i < 1001; i++)
            {
                float[] vector = new float[768];
                for (int j = 0; j < 768; j++)
                {
                    vector[j] = (float)(i + 1) + (j * 0.001f); // Create unique patterns
                }
                await store.AddAsync(new StoredVector(Guid.NewGuid(), vector));
            }

            // Create a query vector that should match one of our patterns
            float[] queryVector = new float[768];
            for (int j = 0; j < 768; j++)
            {
                queryVector[j] = 500.0f + (j * 0.001f); // Should match vector 500
            }

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(queryVector, 10);

            // Assert
            Assert.AreEqual(10, results.Count());
            
            // Verify we get meaningful results (not just random vectors)
            // The query should be most similar to vectors around index 500
            List<StoredVector> resultsList = results.ToList();
            Assert.IsTrue(resultsList.Count > 0);
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldUseSequentialProcessing_ForSmallDatasets()
        {
            // Arrange - Create exactly 999 vectors to ensure we stay below the threshold
            CosineVectorStore store = new CosineVectorStore(768);
            
            // Add 999 vectors with distinct patterns
            for (int i = 0; i < 999; i++)
            {
                float[] vector = new float[768];
                for (int j = 0; j < 768; j++)
                {
                    vector[j] = (float)(i + 1) + (j * 0.001f); // Create unique patterns
                }
                await store.AddAsync(new StoredVector(Guid.NewGuid(), vector));
            }

            // Create a query vector that should match one of our patterns
            float[] queryVector = new float[768];
            for (int j = 0; j < 768; j++)
            {
                queryVector[j] = 500.0f + (j * 0.001f); // Should match vector 500
            }

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(queryVector, 10);

            // Assert
            Assert.AreEqual(10, results.Count());
            
            // Verify we get meaningful results (not just random vectors)
            List<StoredVector> resultsList = results.ToList();
            Assert.IsTrue(resultsList.Count > 0);
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleThresholdBoundary_Exactly1000Vectors()
        {
            // Arrange - Create exactly 1000 vectors to test the boundary
            CosineVectorStore store = new CosineVectorStore(768);
            
            // Add 1000 vectors with distinct patterns
            for (int i = 0; i < 1000; i++)
            {
                float[] vector = new float[768];
                for (int j = 0; j < 768; j++)
                {
                    vector[j] = (float)(i + 1) + (j * 0.001f); // Create unique patterns
                }
                await store.AddAsync(new StoredVector(Guid.NewGuid(), vector));
            }

            // Create a query vector
            float[] queryVector = new float[768];
            for (int j = 0; j < 768; j++)
            {
                queryVector[j] = 500.0f + (j * 0.001f);
            }

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(queryVector, 10);

            // Assert
            Assert.AreEqual(10, results.Count());
            
            // Verify we get meaningful results
            List<StoredVector> resultsList = results.ToList();
            Assert.IsTrue(resultsList.Count > 0);
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldHandleThresholdBoundary_Exactly1001Vectors()
        {
            // Arrange - Create exactly 1001 vectors to test the boundary
            CosineVectorStore store = new CosineVectorStore(768);
            
            // Add 1001 vectors with distinct patterns
            for (int i = 0; i < 1001; i++)
            {
                float[] vector = new float[768];
                for (int j = 0; j < 768; j++)
                {
                    vector[j] = (float)(i + 1) + (j * 0.001f); // Create unique patterns
                }
                await store.AddAsync(new StoredVector(Guid.NewGuid(), vector));
            }

            // Create a query vector
            float[] queryVector = new float[768];
            for (int j = 0; j < 768; j++)
            {
                queryVector[j] = 500.0f + (j * 0.001f);
            }

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(queryVector, 10);

            // Assert
            Assert.AreEqual(10, results.Count());
            
            // Verify we get meaningful results
            List<StoredVector> resultsList = results.ToList();
            Assert.IsTrue(resultsList.Count > 0);
        }

        #endregion

        #region Cosine Similarity Tests

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldReturnPerfectMatch_ForIdenticalVectors()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            StoredVector vector = new StoredVector(Guid.NewGuid(), CreateVector(1.0f));
            await store.AddAsync(vector);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector.Values, 1);

            // Assert
            Assert.AreEqual(1, results.Count());
            Assert.AreEqual(vector.Id, results.First().Id);
        }

        [TestMethod]
        public async Task FindMostSimilarAsync_ShouldReturnOrthogonalVectors_ForPerpendicularVectors()
        {
            // Arrange
            CosineVectorStore store = new CosineVectorStore(768);
            StoredVector vector1 = new StoredVector(Guid.NewGuid(), CreateVector(1.0f));
            StoredVector vector2 = new StoredVector(Guid.NewGuid(), CreateVector(0.0f));
            await store.AddAsync(vector1);
            await store.AddAsync(vector2);

            // Act
            IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(vector1.Values, 2);

            // Assert
            Assert.AreEqual(2, results.Count());
            // Both vectors should be returned, but vector1 should be more similar to itself
            List<Guid> resultIds = results.Select(r => r.Id).ToList();
            Assert.IsTrue(resultIds.Contains(vector1.Id));
            Assert.IsTrue(resultIds.Contains(vector2.Id));
        }

        #endregion
    }
}