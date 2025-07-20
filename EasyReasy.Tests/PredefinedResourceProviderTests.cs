namespace EasyReasy.Tests
{
    [TestClass]
    public class PredefinedResourceProviderTests
    {
        [TestMethod]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange
            Type collectionType = typeof(TestResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();

            // Act
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            // Assert
            Assert.IsNotNull(predefinedProvider);
            Assert.AreEqual(collectionType, predefinedProvider.ResourceCollectionType);
            Assert.AreEqual(provider, predefinedProvider.Provider);
        }

        [TestMethod]
        public void Constructor_WithNullCollectionType_ThrowsArgumentNullException()
        {
            // Arrange
            Type? collectionType = null;
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new PredefinedResourceProvider(
                resourceCollectionType: collectionType!,
                provider: provider));
        }

        [TestMethod]
        public void Constructor_WithNullProvider_ThrowsArgumentNullException()
        {
            // Arrange
            Type collectionType = typeof(TestResourceCollection);
            MockResourceProvider? provider = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider!));
        }

        [TestMethod]
        public void ResourceCollectionType_ReturnsCorrectType()
        {
            // Arrange
            Type expectedType = typeof(TestResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: expectedType,
                provider: provider);

            // Act
            Type actualType = predefinedProvider.ResourceCollectionType;

            // Assert
            Assert.AreEqual(expectedType, actualType);
        }

        [TestMethod]
        public void Provider_ReturnsCorrectProvider()
        {
            // Arrange
            Type collectionType = typeof(TestResourceCollection);
            MockResourceProvider expectedProvider = new MockResourceProvider();
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: expectedProvider);

            // Act
            IResourceProvider actualProvider = predefinedProvider.Provider;

            // Assert
            Assert.AreEqual(expectedProvider, actualProvider);
        }

        [TestMethod]
        public void Constructor_WithInterfaceCollectionType_DoesNotThrow()
        {
            // Arrange
            Type collectionType = typeof(ITestResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            Assert.IsNotNull(predefinedProvider);
        }

        [TestMethod]
        public void Constructor_WithAbstractCollectionType_DoesNotThrow()
        {
            // Arrange
            Type collectionType = typeof(AbstractTestResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            Assert.IsNotNull(predefinedProvider);
        }

        [TestMethod]
        public void Constructor_WithGenericCollectionType_DoesNotThrow()
        {
            // Arrange
            Type collectionType = typeof(GenericTestResourceCollection<string>);
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            Assert.IsNotNull(predefinedProvider);
        }

        [TestMethod]
        public void Constructor_WithNestedCollectionType_DoesNotThrow()
        {
            // Arrange
            Type collectionType = typeof(OuterClass.NestedResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            Assert.IsNotNull(predefinedProvider);
        }

        [TestMethod]
        public void Constructor_WithValueTypeCollectionType_DoesNotThrow()
        {
            // Arrange
            Type collectionType = typeof(ValueTypeResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            Assert.IsNotNull(predefinedProvider);
        }

        [TestMethod]
        public void Constructor_WithEnumCollectionType_DoesNotThrow()
        {
            // Arrange
            Type collectionType = typeof(EnumResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            Assert.IsNotNull(predefinedProvider);
        }

        [TestMethod]
        public void Constructor_WithDelegateCollectionType_DoesNotThrow()
        {
            // Arrange
            Type collectionType = typeof(DelegateResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            Assert.IsNotNull(predefinedProvider);
        }

        [TestMethod]
        public void Constructor_WithArrayCollectionType_DoesNotThrow()
        {
            // Arrange
            Type collectionType = typeof(ArrayResourceCollection);
            MockResourceProvider provider = new MockResourceProvider();

            // Act & Assert
            PredefinedResourceProvider predefinedProvider = new PredefinedResourceProvider(
                resourceCollectionType: collectionType,
                provider: provider);

            Assert.IsNotNull(predefinedProvider);
        }

        // Test resource collections for testing
        public static class TestResourceCollection
        {
            public static readonly Resource TestResource = new Resource("test.txt");
        }

        public interface ITestResourceCollection
        {
            static readonly Resource TestResource = new Resource("test.txt");
        }

        public abstract class AbstractTestResourceCollection
        {
            public static readonly Resource TestResource = new Resource("test.txt");
        }

        public class GenericTestResourceCollection<T>
        {
            public static readonly Resource TestResource = new Resource("test.txt");
        }

        public class OuterClass
        {
            public static class NestedResourceCollection
            {
                public static readonly Resource TestResource = new Resource("test.txt");
            }
        }

        public struct ValueTypeResourceCollection
        {
            public static readonly Resource TestResource = new Resource("test.txt");
        }

        public enum EnumResourceCollection
        {
            TestResource
        }

        public delegate void DelegateResourceCollection();

        public class ArrayResourceCollection
        {
            public static readonly Resource[] TestResources = { new Resource("test.txt") };
        }

        // Mock resource provider for testing
        private class MockResourceProvider : IResourceProvider
        {
            public async Task<bool> ResourceExistsAsync(Resource resource)
            {
                await Task.CompletedTask;
                return resource.Path == "test.txt";
            }

            public async Task<Stream> GetResourceStreamAsync(Resource resource)
            {
                await Task.CompletedTask;
                if (resource.Path == "test.txt")
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes("Test content");
                    return new MemoryStream(bytes);
                }
                throw new FileNotFoundException($"Resource '{resource.Path}' not found.");
            }

            public async Task<byte[]> ReadAsBytesAsync(Resource resource)
            {
                using Stream stream = await GetResourceStreamAsync(resource);
                using MemoryStream memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }

            public async Task<string> ReadAsStringAsync(Resource resource)
            {
                using Stream stream = await GetResourceStreamAsync(resource);
                using StreamReader reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
        }
    }
}