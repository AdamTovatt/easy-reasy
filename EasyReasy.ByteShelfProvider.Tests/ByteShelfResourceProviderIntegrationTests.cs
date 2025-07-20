using ByteShelfClient;
using ByteShelfCommon;

namespace EasyReasy.ByteShelfProvider.Tests
{
    [TestClass]
    public class ByteShelfResourceProviderIntegrationTests
    {
        private const string BaseUrl = "https://localhost:7249/";
        private const string ApiKey = "easyreasy";
        private static string? sub1Id;
        private static string? sub2Id;
        private static string? sub2aId;
        private static string? sub3Id;
        private static string? sub4Id;
        private static string? sub4aId;

        private static readonly List<Guid> createdFileIds = new List<Guid>();
        private static readonly List<string> createdSubTenantIds = new List<string>();
        private static IShelfFileProvider? classShelfProvider;

        // Add a simple mock cache for testing
        private class MockResourceCache : IResourceCache
        {
            private readonly Dictionary<string, byte[]> cache = new();

            public Task<bool> ExistsAsync(string resourcePath)
            {
                return Task.FromResult(cache.ContainsKey(resourcePath));
            }

            public Task<Stream> GetStreamAsync(string resourcePath)
            {
                if (!cache.ContainsKey(resourcePath))
                    throw new FileNotFoundException($"Resource '{resourcePath}' not found in cache.");

                return Task.FromResult<Stream>(new MemoryStream(cache[resourcePath]));
            }

            public Task StoreAsync(string resourcePath, Stream content)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    content.CopyTo(ms);
                    cache[resourcePath] = ms.ToArray();
                }
                return Task.CompletedTask;
            }

            public Task StoreAsync(string resourcePath, byte[] content)
            {
                cache[resourcePath] = content;
                return Task.CompletedTask;
            }

            public Task StoreAsync(string resourcePath, string content)
            {
                cache[resourcePath] = System.Text.Encoding.UTF8.GetBytes(content);
                return Task.CompletedTask;
            }

            public Task<DateTimeOffset?> GetCreationTimeAsync(string resourcePath)
            {
                return Task.FromResult<DateTimeOffset?>(null);
            }
        }

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            // Initialize ByteShelf client
            HttpClient httpClient = HttpShelfFileProvider.CreateHttpClient(BaseUrl);
            classShelfProvider = new HttpShelfFileProvider(httpClient, ApiKey);

            // 1. rootfile.txt at root
            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Root file content")))
            {
                Guid fileId = await classShelfProvider.WriteFileAsync("rootfile.txt", "text/plain", ms);
                createdFileIds.Add(fileId);
            }

            // 2. Sub1/file1.txt
            sub1Id = await classShelfProvider.CreateSubTenantAsync("Sub1");
            createdSubTenantIds.Add(sub1Id);
            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Sub1 file1 content")))
            {
                Guid fileId = await classShelfProvider.WriteFileForTenantAsync(sub1Id, "file1.txt", "text/plain", ms);
                createdFileIds.Add(fileId);
            }

            // 3. Sub2/Sub2A/file2a.txt
            sub2Id = await classShelfProvider.CreateSubTenantAsync("Sub2");
            createdSubTenantIds.Add(sub2Id);
            sub2aId = await classShelfProvider.CreateSubTenantUnderSubTenantAsync(sub2Id, "Sub2A");
            createdSubTenantIds.Add(sub2aId);
            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Sub2A file2a content")))
            {
                Guid fileId = await classShelfProvider.WriteFileForTenantAsync(sub2aId, "file2a.txt", "text/plain", ms);
                createdFileIds.Add(fileId);
            }

            // 4. Sub3/file3a.txt and Sub3/file3b.txt
            sub3Id = await classShelfProvider.CreateSubTenantAsync("Sub3");
            createdSubTenantIds.Add(sub3Id);
            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Sub3 file3a content")))
            {
                Guid fileId = await classShelfProvider.WriteFileForTenantAsync(sub3Id, "file3a.txt", "text/plain", ms);
                createdFileIds.Add(fileId);
            }
            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Sub3 file3b content")))
            {
                Guid fileId = await classShelfProvider.WriteFileForTenantAsync(sub3Id, "file3b.txt", "text/plain", ms);
                createdFileIds.Add(fileId);
            }

            // 5. Sub4/Sub4A/file4a1.txt and Sub4/Sub4A/file4a2.txt
            sub4Id = await classShelfProvider.CreateSubTenantAsync("Sub4");
            createdSubTenantIds.Add(sub4Id);
            sub4aId = await classShelfProvider.CreateSubTenantUnderSubTenantAsync(sub4Id, "Sub4A");
            createdSubTenantIds.Add(sub4aId);
            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Sub4A file4a1 content")))
            {
                Guid fileId = await classShelfProvider.WriteFileForTenantAsync(sub4aId, "file4a1.txt", "text/plain", ms);
                createdFileIds.Add(fileId);
            }
            using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Sub4A file4a2 content")))
            {
                Guid fileId = await classShelfProvider.WriteFileForTenantAsync(sub4aId, "file4a2.txt", "text/plain", ms);
                createdFileIds.Add(fileId);
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (classShelfProvider == null)
                return;

            // Delete all created files
            foreach (Guid fileId in createdFileIds)
            {
                try { await classShelfProvider.DeleteFileAsync(fileId); } catch { }
            }

            // Delete all created subtenants (reverse order for nested)
            foreach (string subTenantId in createdSubTenantIds.AsEnumerable().Reverse())
            {
                try { await classShelfProvider.DeleteSubTenantAsync(subTenantId); } catch { }
            }

            Dictionary<string, TenantInfoResponse> subTenants = await classShelfProvider.GetSubTenantsAsync();

            foreach (string subtenantId in subTenants.Keys)
            {
                await classShelfProvider.DeleteSubTenantAsync(subtenantId);
            }

            IEnumerable<ShelfFileMetadata> files = await classShelfProvider.GetFilesAsync();

            foreach (ShelfFileMetadata file in files)
            {
                await classShelfProvider.DeleteFileAsync(file.Id);
            }
        }

        [TestMethod]
        public async Task ResourceExistsAsync_WithSubTenantHierarchy_ReturnsTrue()
        {
            // sub2/sub2a/file2a.txt
            ByteShelfResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, sub2Id);
            Resource resource = new Resource("Sub2A/file2a.txt");
            bool result = await provider.ResourceExistsAsync(resource);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task GetResourceStreamAsync_WithSubTenantHierarchy_ReturnsCorrectStream()
        {
            // sub2/sub2a/file2a.txt
            ByteShelfResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, sub2Id);
            Resource resource = new Resource("Sub2A/file2a.txt");
            using (Stream stream = await provider.GetResourceStreamAsync(resource))
            using (StreamReader reader = new StreamReader(stream))
            {
                string content = await reader.ReadToEndAsync();
                Assert.AreEqual("Sub2A file2a content", content);
            }
        }

        [TestMethod]
        public async Task GetResourceStreamAsync_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            ByteShelfResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, sub1Id);
            Resource resource = new Resource("nonexistent.txt");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(() =>
                provider.GetResourceStreamAsync(resource));
        }

        [TestMethod]
        public async Task GetResourceStreamAsync_WithNonExistentSubTenant_ThrowsDirectoryNotFoundException()
        {
            // Arrange
            ByteShelfResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, sub1Id);
            Resource resource = new Resource("NonExistentFolder/test.txt");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(() =>
                provider.GetResourceStreamAsync(resource));
        }

        // Test for root-level file
        [TestMethod]
        public async Task ResourceExistsAsync_RootLevelFile_ReturnsTrueAndCorrectContent()
        {
            ByteShelfResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey);
            Resource resource = new Resource("rootfile.txt");

            bool exists = await provider.ResourceExistsAsync(resource);
            Assert.IsTrue(exists);

            string content = await ((IResourceProvider)provider).ReadAsStringAsync(resource);
            Assert.AreEqual("Root file content", content);
        }

        // Parameterized test for subtenant files
        [DataTestMethod]
        [DataRow(null, "rootfile.txt", "Root file content")]
        [DataRow("sub1", "file1.txt", "Sub1 file1 content")]
        [DataRow("sub2a", "file2a.txt", "Sub2A file2a content")]
        [DataRow("sub3", "file3a.txt", "Sub3 file3a content")]
        [DataRow("sub3", "file3b.txt", "Sub3 file3b content")]
        [DataRow("sub4a", "file4a1.txt", "Sub4A file4a1 content")]
        [DataRow("sub4a", "file4a2.txt", "Sub4A file4a2 content")]
        public async Task ResourceExistsAsync_SubtenantFiles_ReturnsTrueAndCorrectContent(string subTenantKey, string fileName, string expectedContent)
        {
            string? subTenantId = subTenantKey switch
            {
                null => null,
                "sub1" => sub1Id,
                "sub2a" => sub2aId,
                "sub3" => sub3Id,
                "sub4a" => sub4aId,
                _ => throw new ArgumentException($"Unknown subtenant key: {subTenantKey}")
            };

            ByteShelfResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, subTenantId);
            Resource resource = new Resource(fileName);

            bool exists = await provider.ResourceExistsAsync(resource);
            Assert.IsTrue(exists);

            string content = await ((IResourceProvider)provider).ReadAsStringAsync(resource);
            Assert.AreEqual(expectedContent, content);
        }

        [DataTestMethod]
        [DataRow("Sub1/file1.txt", "Sub1 file1 content")]
        [DataRow("Sub2/Sub2A/file2a.txt", "Sub2A file2a content")]
        [DataRow("Sub3/file3a.txt", "Sub3 file3a content")]
        [DataRow("Sub3/file3b.txt", "Sub3 file3b content")]
        [DataRow("Sub4/Sub4A/file4a1.txt", "Sub4A file4a1 content")]
        [DataRow("Sub4/Sub4A/file4a2.txt", "Sub4A file4a2 content")]
        public async Task ResourceProvider_ResolvesFilesByPath_FromRoot(string resourcePath, string expectedContent)
        {
            ByteShelfResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey);
            Resource resource = new Resource(resourcePath);
            bool exists = await provider.ResourceExistsAsync(resource);
            Assert.IsTrue(exists);
            string content = await ((IResourceProvider)provider).ReadAsStringAsync(resource);
            Assert.AreEqual(expectedContent, content);
        }

        [TestMethod]
        public async Task GetResourceStreamAsync_UsesCache_WhenFileIsCached()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            FileSystemCache cache = new FileSystemCache(tempDir);
            IResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, cache: cache);
            Resource resource = new Resource("Sub1/file1.txt");

            // Act - First call should download and cache
            string firstResult = await provider.ReadAsStringAsync(resource);

            // Wait to ensure the cache's last write time is newer than the remote
            await Task.Delay(1100);

            // Modify the cached file directly
            string cachedFilePath = Path.Combine(tempDir, "Sub1", "file1.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(cachedFilePath)!);
            File.WriteAllText(cachedFilePath, "MODIFIED CACHE CONTENT");

            // Act - Second call should use cache and return modified content
            string secondResult = await provider.ReadAsStringAsync(resource);

            // Assert
            Assert.AreEqual("Sub1 file1 content", firstResult);
            Assert.AreEqual("MODIFIED CACHE CONTENT", secondResult);
        }

        [TestMethod]
        public async Task GetResourceStreamAsync_InvalidatesCache_WhenRemoteFileIsNewer()
        {
            // Arrange
            Assert.IsNotNull(classShelfProvider); // These are needed
            Assert.IsNotNull(sub1Id);

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            FileSystemCache cache = new FileSystemCache(tempDir);
            IResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, cache: cache);

            // Let's manually create a new resource for this test
            const string fileName = "cacheInvalidationTest.txt";
            const string sub1Name = "Sub1";
            Resource resource = new Resource(Path.Combine(sub1Name, fileName));

            using (MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Cache invalidation test file content")))
            {
                await classShelfProvider.WriteFileForTenantAsync(sub1Id, fileName, "text/plain", memoryStream);
            }

            // Act - First call should download and cache
            string firstResult = await provider.ReadAsStringAsync(resource);
            Assert.AreEqual("Cache invalidation test file content", firstResult);

            string cachedFilePath = Path.Combine(tempDir, sub1Name, fileName);

            Assert.IsTrue(File.Exists(cachedFilePath));

            // Modify the cached file directly
            File.WriteAllText(cachedFilePath, "MODIFIED CACHE CONTENT");

            // Fetch after modifying the cache, before updating the remote
            string secondResult = await provider.ReadAsStringAsync(resource);
            Assert.AreEqual("MODIFIED CACHE CONTENT", secondResult, "Should return modified cache content before remote update");

            // Simulate remote update: delete old file and upload new one with same display name

            // Get the current file to delete it
            IEnumerable<ShelfFileMetadata> files = await classShelfProvider.GetFilesForTenantAsync(sub1Id);
            ShelfFileMetadata? oldFile = files
                .Where(f => string.Equals(f.OriginalFilename, fileName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.CreatedAt)
                .FirstOrDefault();

            if (oldFile != null)
            {
                // Delete the current file
                await classShelfProvider.DeleteFileForTenantAsync(sub1Id, oldFile.Id);
            }

            // Upload new file with same display name
            using (MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("UPDATED REMOTE CONTENT")))
            {
                await classShelfProvider.WriteFileForTenantAsync(sub1Id, fileName, "text/plain", memoryStream);
            }

            // Act - Next call should detect remote is newer and update cache
            string thirdResult = await provider.ReadAsStringAsync(resource);
            Assert.AreEqual("UPDATED REMOTE CONTENT", thirdResult, "Should return updated remote content after remote update");

            // Assert
            Assert.AreEqual("UPDATED REMOTE CONTENT", File.ReadAllText(cachedFilePath)); // cache was updated

            // Arrange again, let's create a second file with the same display name and ensure that that's the one that is used

            // Upload new file with same display name
            using (MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("SUPER UPDATED REMOTE CONTENT")))
            {
                await classShelfProvider.WriteFileForTenantAsync(sub1Id, fileName, "text/plain", memoryStream);
            }

            // Let's see what value we get when reading now
            thirdResult = await provider.ReadAsStringAsync(resource);
            Assert.AreEqual("SUPER UPDATED REMOTE CONTENT", thirdResult, "Should return updated remote content after remote update");
            Assert.AreEqual("SUPER UPDATED REMOTE CONTENT", File.ReadAllText(cachedFilePath)); // cache was updated
        }

        [TestMethod]
        public async Task IsCacheStaleAsync_ReturnsFalse_WhenCacheIsValid()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            FileSystemCache cache = new FileSystemCache(tempDir);
            ICacheableResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, cache: cache);
            Resource resource = new Resource("Sub1/file1.txt");

            // Act - First call should download and cache
            await ((IResourceProvider)provider).ReadAsStringAsync(resource);

            // Act - Check if cache is stale
            bool isStale = await provider.IsCacheStaleAsync(resource);

            // Assert
            Assert.IsFalse(isStale, "Cache should not be stale immediately after caching");
        }

        [TestMethod]
        public async Task IsCacheStaleAsync_ReturnsTrue_WhenRemoteFileIsNewer()
        {
            // Arrange
            Assert.IsNotNull(classShelfProvider);
            Assert.IsNotNull(sub1Id);

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            FileSystemCache cache = new FileSystemCache(tempDir);
            ICacheableResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, cache: cache);

            const string fileName = "staleCacheTest.txt";
            const string sub1Name = "Sub1";
            Resource resource = new Resource(Path.Combine(sub1Name, fileName));

            // Create initial file
            using (MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Initial content")))
            {
                await classShelfProvider.WriteFileForTenantAsync(sub1Id, fileName, "text/plain", memoryStream);
            }

            // Act - First call should download and cache
            await ((IResourceProvider)provider).ReadAsStringAsync(resource);

            // Update remote file
            using (MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Updated content")))
            {
                await classShelfProvider.WriteFileForTenantAsync(sub1Id, fileName, "text/plain", memoryStream);
            }

            // Act - Check if cache is stale
            bool isStale = await provider.IsCacheStaleAsync(resource);

            // Assert
            Assert.IsTrue(isStale, "Cache should be stale when remote file is newer");
        }

        [TestMethod]
        public async Task UpdateCacheAsync_UpdatesCache_WithLatestContent()
        {
            // Arrange
            Assert.IsNotNull(classShelfProvider);
            Assert.IsNotNull(sub1Id);

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            FileSystemCache cache = new FileSystemCache(tempDir);
            ICacheableResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, cache: cache);

            const string fileName = "updateCacheTest.txt";
            const string sub1Name = "Sub1";
            Resource resource = new Resource(Path.Combine(sub1Name, fileName));

            // Create initial file
            const string initialContent = "Initial content";
            using (MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(initialContent)))
            {
                await classShelfProvider.WriteFileForTenantAsync(sub1Id, fileName, "text/plain", memoryStream);
            }

            // Act - First call should download and cache
            string downloadedInitialContent = await ((IResourceProvider)provider).ReadAsStringAsync(resource);
            Assert.AreEqual(initialContent, downloadedInitialContent);

            IResourceCache? resourceCache = provider.GetCache();
            Assert.IsNotNull(resourceCache);

            using (Stream cacheStream = await resourceCache.GetStreamAsync(resource))
            {
                using (StreamReader reader = new StreamReader(cacheStream))
                {
                    string content = await reader.ReadToEndAsync();
                    Assert.AreEqual(initialContent, content);
                }
            }

            // Update remote file
            const string updatedContent = "Updated content";
            using (MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(updatedContent)))
            {
                await classShelfProvider.WriteFileForTenantAsync(sub1Id, fileName, "text/plain", memoryStream);
            }

            // Act - Update cache
            await provider.UpdateCacheAsync(resource);

            // Assert - Cache should now contain updated content
            string cachedContent = await ((IResourceProvider)provider).ReadAsStringAsync(resource);
            Assert.AreEqual("Updated content", cachedContent, "Cache should contain updated content after UpdateCacheAsync");

            using (Stream cacheStream = await resourceCache.GetStreamAsync(resource))
            {
                using (StreamReader reader = new StreamReader(cacheStream))
                {
                    string content = await reader.ReadToEndAsync();
                    Assert.AreEqual(updatedContent, content);
                }
            }
        }

        [TestMethod]
        public async Task ResourceExistsAsync_ReturnsFalse_ForRemotelyDeletedFile()
        {
            // Arrange
            Assert.IsNotNull(classShelfProvider);
            Assert.IsNotNull(sub1Id);

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            FileSystemCache cache = new FileSystemCache(tempDir);
            IResourceProvider provider = new ByteShelfResourceProvider(BaseUrl, ApiKey, cache: cache);

            const string fileName = "existsStaleCacheTest.txt";
            const string sub1Name = "Sub1";
            Resource resource = new Resource(Path.Combine(sub1Name, fileName));

            // Create initial file
            using (MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Initial content")))
            {
                await classShelfProvider.WriteFileForTenantAsync(sub1Id, fileName, "text/plain", memoryStream);
            }

            // Act - First call should download and cache
            bool firstExists = await provider.ResourceExistsAsync(resource);
            Assert.IsTrue(firstExists, "Resource should exist initially");

            // Delete remote file
            IEnumerable<ShelfFileMetadata> files = await classShelfProvider.GetFilesForTenantAsync(sub1Id);
            ShelfFileMetadata? file = files
                .Where(f => string.Equals(f.OriginalFilename, fileName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.CreatedAt)
                .FirstOrDefault();

            if (file != null)
            {
                await classShelfProvider.DeleteFileForTenantAsync(sub1Id, file.Id);
            }

            // Act - Check if resource exists (should ignore stale cache and check remote)
            bool secondExists = await provider.ResourceExistsAsync(resource);

            // Assert
            Assert.IsFalse(secondExists, "ResourceExistsAsync should return false when remote file is deleted, even if cached");
        }
    }
}