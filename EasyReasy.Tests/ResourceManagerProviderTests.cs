using EasyReasy.Tests.TestProviders;
using EasyReasy.Tests.TestResourceCollections;
using System.Reflection;

namespace EasyReasy.Tests
{
    [TestClass]
    public class ResourceManagerProviderTests
    {
        [TestMethod]
        public async Task GetProviderForResource_WithValidResource_ReturnsProvider()
        {
            // Arrange
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly(), predefinedProvider);
            Resource testResource = SharedTestResourceCollection.TestResource;

            // Act
            IResourceProvider provider = manager.GetProviderForResource(testResource);

            // Assert
            Assert.IsNotNull(provider);
            Assert.IsInstanceOfType(provider, typeof(ParameterizedFakeResourceProvider));
        }

        [TestMethod]
        public async Task GetProviderForResource_WithAutoRegisteredResource_ReturnsProvider()
        {
            // Arrange
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly(), predefinedProvider);
            Resource testResource = AutoRegisteredResourceCollection.TestResource;

            // Act
            IResourceProvider provider = manager.GetProviderForResource(testResource);

            // Assert
            Assert.IsNotNull(provider);
            Assert.IsInstanceOfType(provider, typeof(ProviderFakeResourceProvider));
        }

        [TestMethod]
        public async Task GetProviderForResource_WithResourceNotInCollection_ThrowsException()
        {
            // Arrange
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly(), predefinedProvider);
            Resource manualResource = new Resource("manual.txt");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => manager.GetProviderForResource(manualResource));

            Assert.IsTrue(exception.Message.Contains("Resource not found in any collection"));
            Assert.IsTrue(exception.Message.Contains("manual.txt"));
        }

        [TestMethod]
        public async Task GetProviderForResource_WithNoProviderRegistered_ThrowsException()
        {
            // Arrange
            // Create a resource collection that requires a predefined provider but don't provide one
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly(), predefinedProvider);
            Resource testResource = new Resource("nonexistent.txt");

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => manager.GetProviderForResource(testResource));

            Assert.IsTrue(exception.Message.Contains("Resource not found in any collection"));
            Assert.IsTrue(exception.Message.Contains("nonexistent.txt"));
        }

        // Test resource collection that can be auto-registered (parameterless constructor)
        [ResourceCollection(typeof(ProviderFakeResourceProvider))]
        public static class AutoRegisteredResourceCollection
        {
            public static readonly Resource TestResource = new Resource("provider-auto.txt");
        }
    }
}