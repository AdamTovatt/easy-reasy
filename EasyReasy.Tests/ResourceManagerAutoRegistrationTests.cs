using EasyReasy.Tests.TestProviders;
using EasyReasy.Tests.TestResourceCollections;
using System.Reflection;

namespace EasyReasy.Tests
{
    [TestClass]
    public class ResourceManagerAutoRegistrationTests
    {
        [TestMethod]
        public async Task AutoRegistration_WithAllOptionalConstructor_Works()
        {
            // Arrange: We still need to provide the ParameterizedFakeResourceProvider
            // but the other providers should auto-register
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));

            ResourceManager manager = await ResourceManager.CreateInstanceAsync(predefinedProvider);
            Resource testResource = AllOptionalResourceCollection.TestResource;

            // Act
            bool exists = await manager.ResourceExistsAsync(testResource);
            string content = await manager.ReadAsStringAsync(testResource);

            // Assert
            Assert.IsTrue(exists);
            Assert.AreEqual("All optional content", content);
        }

        [TestMethod]
        public async Task AutoRegistration_WithAssemblyParameter_InjectsCorrectAssembly()
        {
            // Arrange
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));

            Assembly expectedAssembly = Assembly.GetExecutingAssembly();
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(expectedAssembly, predefinedProvider);

            // Act
            // The provider should be auto-registered with the correct assembly
            // We can verify this by checking if the resource collection's provider was created correctly
            bool resourceExists = manager.ResourceExistsAsync(AssemblyAwareResourceCollection.TestResource).Result;

            // Assert
            Assert.IsTrue(resourceExists);
            // The provider should have received the correct assembly
            Assert.AreEqual(expectedAssembly, AssemblyAwareTestProvider.LastReceivedAssembly);
        }

        [TestMethod]
        public async Task VerifyResourceMappings_WithAutoRegisteredProviders_DoesNotThrow()
        {
            // Arrange
            // Provide predefined provider for SharedTestResourceCollection (which needs it)
            // AutoRegisteredResourceCollection should be auto-registered
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly(), predefinedProvider);

            // Act & Assert
            // Should not throw because:
            // - SharedTestResourceCollection has predefined provider
            // - AutoRegisteredResourceCollection is auto-registered (FakeResourceProvider has parameterless constructor)
            await manager.VerifyResourceMappingsAsync();
        }

        // Test resource collection that can be auto-registered (parameterless constructor)
        [ResourceCollection(typeof(AutoRegistrationFakeResourceProvider))]
        public static class AutoRegisteredResourceCollection
        {
            public static readonly Resource TestResource = new Resource("auto-registration-auto.txt");
        }

        // Resource collection using a provider with all-optional constructor parameters
        [ResourceCollection(typeof(AllOptionalFakeResourceProvider))]
        public static class AllOptionalResourceCollection
        {
            public static readonly Resource TestResource = new Resource("alloptional.txt");
        }

        // Resource collection using a provider that receives an Assembly parameter
        [ResourceCollection(typeof(AssemblyAwareTestProvider))]
        public static class AssemblyAwareResourceCollection
        {
            public static readonly Resource TestResource = new Resource("assemblyaware.txt");
        }
    }
}