namespace EasyReasy.VectorStorage.Tests
{
    [TestClass]
    public class CosineVectorStorePerformanceTests
    {
        #region Constants

        private const int LARGE_DATASET_SIZE = 10_000;
        private const int VECTOR_DIMENSION = 768;
        private const int SEARCH_QUERY_COUNT = 1_00;

        #endregion

        #region Test Helpers

        private static float[] CreateVector(float value, int dimension = VECTOR_DIMENSION)
        {
            float[] vec = new float[dimension];
            Array.Fill(vec, value);
            return vec;
        }

        private static float[] CreateRandomVector(int dimension = VECTOR_DIMENSION, float scale = 1f)
        {
            Random rng = new Random();
            float[] vec = new float[dimension];
            for (int i = 0; i < dimension; i++)
                vec[i] = (float)(rng.NextDouble() * scale);
            return vec;
        }

        private static List<StoredVector> CreateLargeDataset(int count)
        {
            List<StoredVector> vectors = new List<StoredVector>(count);
            for (int i = 0; i < count; i++)
            {
                vectors.Add(new StoredVector(Guid.NewGuid(), CreateRandomVector()));
            }

            return vectors;
        }

        private static List<float[]> CreateSearchQueries(int count)
        {
            List<float[]> queries = new List<float[]>(count);
            for (int i = 0; i < count; i++)
            {
                queries.Add(CreateRandomVector());
            }

            return queries;
        }

        #endregion

        [TestMethod]
        public async Task PerformanceTest_LargeDatasetOperations()
        {
            Console.WriteLine($"=== Performance Test with {LARGE_DATASET_SIZE:N0} vectors ===");
            Console.WriteLine($"Vector dimension: {VECTOR_DIMENSION}");
            Console.WriteLine();

            // Create large dataset
            Console.WriteLine("Creating large dataset...");
            List<StoredVector> vectors = CreateLargeDataset(LARGE_DATASET_SIZE);
            Console.WriteLine($"Created {LARGE_DATASET_SIZE:N0} vectors");

            // Test insertion performance with memory tracking
            Console.WriteLine("\n--- Insertion Performance ---");
            CosineVectorStore store = new CosineVectorStore(VECTOR_DIMENSION);

            // Get memory before insertion
            long memoryBeforeInsertion = GC.GetTotalMemory(true);
            Console.WriteLine($"Memory before insertion: {memoryBeforeInsertion / 1024 / 1024:N0} MB");

            System.Diagnostics.Stopwatch insertionTimer = System.Diagnostics.Stopwatch.StartNew();
            foreach (StoredVector vector in vectors)
            {
                await store.AddAsync(vector);
            }

            insertionTimer.Stop();

            // Get memory after insertion
            long memoryAfterInsertion = GC.GetTotalMemory(true);
            long insertionMemoryDelta = memoryAfterInsertion - memoryBeforeInsertion;

            long insertionTimeMs = insertionTimer.ElapsedMilliseconds;
            double insertionTimePerVectorNs = (double)insertionTimer.ElapsedTicks * 1_000_000 / TimeSpan.TicksPerSecond / LARGE_DATASET_SIZE;

            Console.WriteLine($"Memory after insertion: {memoryAfterInsertion / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory delta for insertion: {insertionMemoryDelta / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory per vector: {insertionMemoryDelta / (double)LARGE_DATASET_SIZE / 1024 / 1024:F2} MB");
            Console.WriteLine($"Total insertion time: {insertionTimeMs:N0} ms");
            Console.WriteLine($"Average time per vector: {insertionTimePerVectorNs:F2} ns");
            Console.WriteLine($"Insertion rate: {LARGE_DATASET_SIZE / (insertionTimeMs / 1000.0):F0} vectors/second");

            // Test save performance
            Console.WriteLine("\n--- Save Performance ---");
            string tempFilePath = Path.GetTempFileName();

            System.Diagnostics.Stopwatch saveTimer = System.Diagnostics.Stopwatch.StartNew();
            using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create))
            {
                await store.SaveAsync(fileStream);
            }

            saveTimer.Stop();

            long saveTimeMs = saveTimer.ElapsedMilliseconds;
            Console.WriteLine($"Save time: {saveTimeMs:N0} ms");
            Console.WriteLine($"Save rate: {LARGE_DATASET_SIZE / (saveTimeMs / 1000.0):F0} vectors/second");

            // Test load performance with memory tracking
            Console.WriteLine("\n--- Load Performance ---");
            CosineVectorStore loadedStore = new CosineVectorStore(VECTOR_DIMENSION);

            // Get memory before loading
            long memoryBeforeLoad = GC.GetTotalMemory(true);
            Console.WriteLine($"Memory before loading: {memoryBeforeLoad / 1024 / 1024:N0} MB");

            System.Diagnostics.Stopwatch loadTimer = System.Diagnostics.Stopwatch.StartNew();
            using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Open))
            {
                await loadedStore.LoadAsync(fileStream);
            }

            loadTimer.Stop();

            // Get memory after loading
            long memoryAfterLoad = GC.GetTotalMemory(true);
            long loadMemoryDelta = memoryAfterLoad - memoryBeforeLoad;

            long loadTimeMs = loadTimer.ElapsedMilliseconds;
            Console.WriteLine($"Memory after loading: {memoryAfterLoad / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory delta for loading: {loadMemoryDelta / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory per vector (loaded): {loadMemoryDelta / (double)LARGE_DATASET_SIZE / 1024 / 1024:F2} MB");
            Console.WriteLine($"Load time: {loadTimeMs:N0} ms");
            Console.WriteLine($"Load rate: {LARGE_DATASET_SIZE / (loadTimeMs / 1000.0):F0} vectors/second");

            // Test search performance
            Console.WriteLine("\n--- Search Performance ---");
            List<float[]> searchQueries = CreateSearchQueries(SEARCH_QUERY_COUNT);

            System.Diagnostics.Stopwatch searchTimer = System.Diagnostics.Stopwatch.StartNew();
            foreach (float[] query in searchQueries)
            {
                IEnumerable<StoredVector> results = await loadedStore.FindMostSimilarAsync(query, 10);
                // Ensure we actually consume the results
                int resultCount = results.Count();
            }

            searchTimer.Stop();

            long searchTimeMs = searchTimer.ElapsedMilliseconds;
            double searchTimePerQueryMs = (double)searchTimeMs / SEARCH_QUERY_COUNT;
            Console.WriteLine($"Total search time for {SEARCH_QUERY_COUNT:N0} queries: {searchTimeMs:N0} ms");
            Console.WriteLine($"Average time per search query: {searchTimePerQueryMs:F2} ms");
            Console.WriteLine($"Search rate: {SEARCH_QUERY_COUNT / (searchTimeMs / 1000.0):F0} queries/second");

            // Test search performance with original store (to compare with loaded store)
            Console.WriteLine("\n--- Original Store Search Performance ---");
            System.Diagnostics.Stopwatch originalSearchTimer = System.Diagnostics.Stopwatch.StartNew();
            foreach (float[] query in searchQueries)
            {
                IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(query, 10);
                int resultCount = results.Count();
            }

            originalSearchTimer.Stop();

            long originalSearchTimeMs = originalSearchTimer.ElapsedMilliseconds;
            double originalSearchTimePerQueryMs = (double)originalSearchTimeMs / SEARCH_QUERY_COUNT;
            Console.WriteLine($"Original store search time for {SEARCH_QUERY_COUNT:N0} queries: {originalSearchTimeMs:N0} ms");
            Console.WriteLine($"Original store average time per search query: {originalSearchTimePerQueryMs:F2} ms");

            // Memory comparison between original and loaded stores
            Console.WriteLine("\n--- Memory Comparison ---");
            Console.WriteLine($"Original store memory usage: {insertionMemoryDelta / 1024 / 1024:N0} MB");
            Console.WriteLine($"Loaded store memory usage: {loadMemoryDelta / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory difference: {(loadMemoryDelta - insertionMemoryDelta) / 1024 / 1024:N0} MB");

            // Cleanup
            try
            {
                File.Delete(tempFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            Console.WriteLine("\n=== Performance Test Complete ===");

            // Assertions to ensure the operations completed successfully
            Assert.AreEqual(LARGE_DATASET_SIZE, vectors.Count);
            Assert.IsTrue(insertionTimeMs > 0);
            Assert.IsTrue(saveTimeMs > 0);
            Assert.IsTrue(loadTimeMs > 0);
            Assert.IsTrue(searchTimeMs > 0);
            Assert.IsTrue(insertionMemoryDelta > 0);
            Assert.IsTrue(loadMemoryDelta > 0);
        }

        [TestMethod]
        public async Task PerformanceTest_MemoryUsage()
        {
            Console.WriteLine($"=== Memory Usage Test with {LARGE_DATASET_SIZE:N0} vectors ===");

            // Get initial memory usage
            long initialMemory = GC.GetTotalMemory(true);
            Console.WriteLine($"Initial memory: {initialMemory / 1024 / 1024:N0} MB");

            // Create and populate store
            CosineVectorStore? store = new CosineVectorStore(VECTOR_DIMENSION);
            List<StoredVector>? vectors = CreateLargeDataset(LARGE_DATASET_SIZE);

            foreach (StoredVector vector in vectors)
            {
                await store.AddAsync(vector);
            }

            // Get memory after population
            long populatedMemory = GC.GetTotalMemory(true);
            Console.WriteLine($"Memory after populating store: {populatedMemory / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory used by store: {(populatedMemory - initialMemory) / 1024 / 1024:N0} MB");

            // Perform some searches to see if memory usage changes
            List<float[]>? searchQueries = CreateSearchQueries(100);
            foreach (float[] query in searchQueries)
            {
                IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(query, 10);
                int resultCount = results.Count();
            }

            // Get memory after searches
            long afterSearchMemory = GC.GetTotalMemory(true);
            Console.WriteLine($"Memory after searches: {afterSearchMemory / 1024 / 1024:N0} MB");

            // Clear references and force garbage collection
            store = null;
            vectors = null;
            searchQueries = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long finalMemory = GC.GetTotalMemory(true);
            Console.WriteLine($"Memory after cleanup: {finalMemory / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory reclaimed: {(afterSearchMemory - finalMemory) / 1024 / 1024:N0} MB");

            Console.WriteLine("=== Memory Usage Test Complete ===");

            // Assertions
            Assert.IsTrue(populatedMemory > initialMemory);
            Assert.IsTrue(afterSearchMemory >= populatedMemory);
        }

        [TestMethod]
        public async Task PerformanceTest_ConcurrentOperations()
        {
            Console.WriteLine($"=== Concurrent Operations Test with {LARGE_DATASET_SIZE:N0} vectors ===");
            Console.WriteLine($"Vector dimension: {VECTOR_DIMENSION}");
            Console.WriteLine($"Search queries: {SEARCH_QUERY_COUNT:N0}");
            Console.WriteLine();

            // Create large dataset
            Console.WriteLine("Creating large dataset...");
            List<StoredVector> vectors = CreateLargeDataset(LARGE_DATASET_SIZE);
            List<float[]> searchQueries = CreateSearchQueries(SEARCH_QUERY_COUNT);
            Console.WriteLine($"Created {LARGE_DATASET_SIZE:N0} vectors and {SEARCH_QUERY_COUNT:N0} search queries");

            // Test concurrent insertion with memory tracking
            Console.WriteLine("\n--- Concurrent Insertion Performance ---");
            CosineVectorStore store = new CosineVectorStore(VECTOR_DIMENSION);

            // Get memory before concurrent insertion
            long memoryBeforeConcurrentInsertion = GC.GetTotalMemory(true);
            Console.WriteLine($"Memory before concurrent insertion: {memoryBeforeConcurrentInsertion / 1024 / 1024:N0} MB");

            System.Diagnostics.Stopwatch concurrentInsertionTimer = System.Diagnostics.Stopwatch.StartNew();

            List<Task> insertionTasks = new List<Task>();
            foreach (StoredVector vector in vectors)
            {
                insertionTasks.Add(store.AddAsync(vector));
            }

            await Task.WhenAll(insertionTasks);

            concurrentInsertionTimer.Stop();

            // Get memory after concurrent insertion
            long memoryAfterConcurrentInsertion = GC.GetTotalMemory(true);
            long concurrentInsertionMemoryDelta = memoryAfterConcurrentInsertion - memoryBeforeConcurrentInsertion;

            long concurrentInsertionTimeMs = concurrentInsertionTimer.ElapsedMilliseconds;
            double concurrentInsertionTimePerVectorNs = (double)concurrentInsertionTimer.ElapsedTicks * 1_000_000 / TimeSpan.TicksPerSecond / LARGE_DATASET_SIZE;

            Console.WriteLine($"Memory after concurrent insertion: {memoryAfterConcurrentInsertion / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory delta for concurrent insertion: {concurrentInsertionMemoryDelta / 1024 / 1024:N0} MB");
            Console.WriteLine($"Memory per vector (concurrent): {concurrentInsertionMemoryDelta / (double)LARGE_DATASET_SIZE / 1024 / 1024:F2} MB");
            Console.WriteLine($"Total concurrent insertion time: {concurrentInsertionTimeMs:N0} ms");
            Console.WriteLine($"Average time per vector (concurrent): {concurrentInsertionTimePerVectorNs:F2} ns");
            Console.WriteLine($"Concurrent insertion rate: {LARGE_DATASET_SIZE / (concurrentInsertionTimeMs / 1000.0):F0} vectors/second");

            // Test concurrent search with detailed metrics
            Console.WriteLine("\n--- Concurrent Search Performance ---");
            System.Diagnostics.Stopwatch concurrentSearchTimer = System.Diagnostics.Stopwatch.StartNew();

            List<Task> searchTasks = new List<Task>();
            foreach (float[] query in searchQueries)
            {
                searchTasks.Add(Task.Run(async () =>
                {
                    IEnumerable<StoredVector> results = await store.FindMostSimilarAsync(query, 10);
                    int resultCount = results.Count();
                }));
            }

            await Task.WhenAll(searchTasks);

            concurrentSearchTimer.Stop();

            long concurrentSearchTimeMs = concurrentSearchTimer.ElapsedMilliseconds;
            double concurrentSearchTimePerQueryMs = (double)concurrentSearchTimeMs / SEARCH_QUERY_COUNT;
            double concurrentSearchTimePerQueryNs = (double)concurrentSearchTimer.ElapsedTicks * 1_000_000 / TimeSpan.TicksPerSecond / SEARCH_QUERY_COUNT;

            Console.WriteLine($"Total concurrent search time for {SEARCH_QUERY_COUNT:N0} queries: {concurrentSearchTimeMs:N0} ms");
            Console.WriteLine($"Average time per search query (concurrent): {concurrentSearchTimePerQueryMs:F2} ms");
            Console.WriteLine($"Average time per search query (concurrent): {concurrentSearchTimePerQueryNs:F2} ns");
            Console.WriteLine($"Concurrent search rate: {SEARCH_QUERY_COUNT / (concurrentSearchTimeMs / 1000.0):F0} queries/second");

            // Compare with sequential operations for reference
            Console.WriteLine("\n--- Sequential Operations Comparison ---");
            
            // Sequential insertion
            CosineVectorStore sequentialStore = new CosineVectorStore(VECTOR_DIMENSION);
            System.Diagnostics.Stopwatch sequentialInsertionTimer = System.Diagnostics.Stopwatch.StartNew();
            
            foreach (StoredVector vector in vectors)
            {
                await sequentialStore.AddAsync(vector);
            }
            
            sequentialInsertionTimer.Stop();
            long sequentialInsertionTimeMs = sequentialInsertionTimer.ElapsedMilliseconds;
            double sequentialInsertionTimePerVectorNs = (double)sequentialInsertionTimer.ElapsedTicks * 1_000_000 / TimeSpan.TicksPerSecond / LARGE_DATASET_SIZE;
            
            Console.WriteLine($"Sequential insertion time: {sequentialInsertionTimeMs:N0} ms");
            Console.WriteLine($"Average time per vector (sequential): {sequentialInsertionTimePerVectorNs:F2} ns");
            Console.WriteLine($"Sequential insertion rate: {LARGE_DATASET_SIZE / (sequentialInsertionTimeMs / 1000.0):F0} vectors/second");
            
            // Sequential search
            System.Diagnostics.Stopwatch sequentialSearchTimer = System.Diagnostics.Stopwatch.StartNew();
            foreach (float[] query in searchQueries)
            {
                IEnumerable<StoredVector> results = await sequentialStore.FindMostSimilarAsync(query, 10);
                int resultCount = results.Count();
            }
            sequentialSearchTimer.Stop();
            
            long sequentialSearchTimeMs = sequentialSearchTimer.ElapsedMilliseconds;
            double sequentialSearchTimePerQueryMs = (double)sequentialSearchTimeMs / SEARCH_QUERY_COUNT;
            
            Console.WriteLine($"Sequential search time for {SEARCH_QUERY_COUNT:N0} queries: {sequentialSearchTimeMs:N0} ms");
            Console.WriteLine($"Average time per search query (sequential): {sequentialSearchTimePerQueryMs:F2} ms");
            Console.WriteLine($"Sequential search rate: {SEARCH_QUERY_COUNT / (sequentialSearchTimeMs / 1000.0):F0} queries/second");

            // Performance comparison
            Console.WriteLine("\n--- Performance Comparison ---");
            double insertionSpeedup = (double)sequentialInsertionTimeMs / concurrentInsertionTimeMs;
            double searchSpeedup = (double)sequentialSearchTimeMs / concurrentSearchTimeMs;
            
            Console.WriteLine($"Insertion speedup (concurrent vs sequential): {insertionSpeedup:F2}x");
            Console.WriteLine($"Search speedup (concurrent vs sequential): {searchSpeedup:F2}x");
            Console.WriteLine($"Insertion efficiency: {insertionSpeedup / Environment.ProcessorCount:F2}x per core");
            Console.WriteLine($"Search efficiency: {searchSpeedup / Environment.ProcessorCount:F2}x per core");

            Console.WriteLine("\n=== Concurrent Operations Test Complete ===");

            // Assertions
            Assert.IsTrue(concurrentInsertionTimer.ElapsedMilliseconds > 0);
            Assert.IsTrue(concurrentSearchTimer.ElapsedMilliseconds > 0);
            Assert.IsTrue(sequentialInsertionTimer.ElapsedMilliseconds > 0);
            Assert.IsTrue(sequentialSearchTimer.ElapsedMilliseconds > 0);
            Assert.IsTrue(concurrentInsertionMemoryDelta > 0);
        }
    }
}