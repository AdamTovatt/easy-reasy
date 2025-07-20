using EasyReasy.ByteShelfProvider;

namespace EasyReasy.Tests
{
    [TestClass]
    public class ByteShelfResourceProviderCreationTests
    {
        // Mock cache implementation for testing
        private class MockResourceCache : IResourceCache
        {
            public Task<bool> ExistsAsync(string resourcePath)
            {
                return Task.FromResult(false);
            }

            public Task<Stream> GetStreamAsync(string resourcePath)
            {
                throw new NotImplementedException();
            }

            public Task StoreAsync(string resourcePath, Stream content)
            {
                return Task.CompletedTask;
            }

            public Task StoreAsync(string resourcePath, byte[] content)
            {
                return Task.CompletedTask;
            }

            public Task StoreAsync(string resourcePath, string content)
            {
                return Task.CompletedTask;
            }

            public Task<DateTimeOffset?> GetCreationTimeAsync(string resourcePath)
            {
                return Task.FromResult<DateTimeOffset?>(null);
            }
        }

        [TestMethod]
        public void Create_WithValidParameters_ReturnsPredefinedProvider()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string apiKey = "test-api-key";

            // Act
            PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey);

            // Assert
            Assert.IsNotNull(provider);
            Assert.AreEqual(typeof(TestResourceCollection), provider.ResourceCollectionType);
            Assert.IsNotNull(provider.Provider);
            Assert.IsInstanceOfType(provider.Provider, typeof(ByteShelfResourceProvider));
        }

        [TestMethod]
        public void Create_WithNullBaseUrl_ThrowsArgumentException()
        {
            // Arrange
            string? baseUrl = null;
            string apiKey = "test-api-key";

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl!,
                apiKey: apiKey));
        }

        [TestMethod]
        public void Create_WithNullApiKey_ThrowsArgumentException()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string? apiKey = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey!));
        }

        [TestMethod]
        public void Create_WithEmptyBaseUrl_ThrowsArgumentException()
        {
            // Arrange
            string baseUrl = "";
            string apiKey = "test-api-key";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey));
        }

        [TestMethod]
        public void Create_WithEmptyApiKey_ThrowsArgumentException()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string apiKey = "";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey));
        }

        [TestMethod]
        public void Create_WithWhitespaceBaseUrl_ThrowsArgumentException()
        {
            // Arrange
            string baseUrl = "   ";
            string apiKey = "test-api-key";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey));
        }

        [TestMethod]
        public void Create_WithWhitespaceApiKey_ThrowsArgumentException()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string apiKey = "   ";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey));
        }

        [TestMethod]
        public void Create_WithValidHttpsUrl_DoesNotThrow()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string apiKey = "test-api-key";

            // Act & Assert
            PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Create_WithValidHttpsUrlWithPath_DoesNotThrow()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com/v1";
            string apiKey = "test-api-key";

            // Act & Assert
            PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Create_WithValidHttpsUrlWithTrailingSlash_DoesNotThrow()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com/";
            string apiKey = "test-api-key";

            // Act & Assert
            PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Create_WithNullCache_DoesNotThrow()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string apiKey = "test-api-key";
            IResourceCache? cache = null;

            // Act & Assert
            PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey,
                cache: cache);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Create_WithMockCache_DoesNotThrow()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string apiKey = "test-api-key";
            IResourceCache cache = new MockResourceCache();

            // Act & Assert
            PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey,
                cache: cache);

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public void Create_WithRootSubTenantId_ReturnsPredefinedProvider()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string apiKey = "test-api-key";
            string rootSubTenantId = "tenant-123";

            // Act & Assert
            PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey,
                rootSubTenantId: rootSubTenantId);

            Assert.IsNotNull(provider);
            Assert.AreEqual(typeof(TestResourceCollection), provider.ResourceCollectionType);
            Assert.IsNotNull(provider.Provider);
            Assert.IsInstanceOfType(provider.Provider, typeof(ByteShelfResourceProvider));
        }

        [TestMethod]
        public void Create_WithAllParameters_ReturnsPredefinedProvider()
        {
            // Arrange
            string baseUrl = "https://api.byteshelf.com";
            string apiKey = "test-api-key";
            string rootSubTenantId = "tenant-123";
            IResourceCache cache = new MockResourceCache();

            // Act & Assert
            PredefinedResourceProvider provider = ByteShelfResourceProvider.CreatePredefined(
                resourceCollectionType: typeof(TestResourceCollection),
                baseUrl: baseUrl,
                apiKey: apiKey,
                rootSubTenantId: rootSubTenantId,
                cache: cache);

            Assert.IsNotNull(provider);
            Assert.AreEqual(typeof(TestResourceCollection), provider.ResourceCollectionType);
            Assert.IsNotNull(provider.Provider);
            Assert.IsInstanceOfType(provider.Provider, typeof(ByteShelfResourceProvider));
        }

        // Test resource collection for testing
        public static class TestResourceCollection
        {
            public static readonly Resource TestResource = new Resource("test.txt");
        }
    }
}