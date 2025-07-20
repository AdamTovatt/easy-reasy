using EasyReasy.Tests.TestProviders;
using EasyReasy.Tests.TestResourceCollections;
using System.Reflection;

namespace EasyReasy.Tests
{
    [TestClass]
    public class ResourceManagerValidationTests
    {
        [TestMethod]
        public async Task VerifyResourceMappings_WithValidProviders_DoesNotThrow()
        {
            // Arrange
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly(), predefinedProvider);

            // Act & Assert
            await manager.VerifyResourceMappingsAsync(); // Should not throw when providers are properly registered
        }

        [TestMethod]
        public async Task CreateInstance_WithMissingProvider_ThrowsException()
        {
            // Arrange & Act & Assert
            // Create manager with no predefined providers
            // AutoRegisteredResourceCollection will be auto-registered (FakeResourceProvider has parameterless constructor)
            // SharedTestResourceCollection will NOT be auto-registered (ParameterizedFakeResourceProvider has constructor parameters)
            // Should throw because SharedTestResourceCollection has no registered provider
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly()));
        }

        [TestMethod]
        public void ValidateResourcePathUniqueness_WithDuplicatePaths_ThrowsException()
        {
            // Arrange
            Dictionary<string, List<Type>> resourcePathToCollections = new Dictionary<string, List<Type>>
            {
                ["duplicate.txt"] = new List<Type> { typeof(DuplicatePathCollection1), typeof(DuplicatePathCollection2) },
                ["unique.txt"] = new List<Type> { typeof(UniquePathCollection) }
            };

            // Act & Assert
            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
                () => ValidateResourcePathUniqueness(resourcePathToCollections));

            Assert.IsTrue(exception.Message.Contains("Resource path conflicts detected"));
            Assert.IsTrue(exception.Message.Contains("duplicate.txt"));
            Assert.IsTrue(exception.Message.Contains("DuplicatePathCollection1"));
            Assert.IsTrue(exception.Message.Contains("DuplicatePathCollection2"));
        }

        [TestMethod]
        public void ValidateResourcePathUniqueness_WithUniquePaths_DoesNotThrow()
        {
            // Arrange
            Dictionary<string, List<Type>> resourcePathToCollections = new Dictionary<string, List<Type>>
            {
                ["unique1.txt"] = new List<Type> { typeof(UniquePathCollection1) },
                ["unique2.txt"] = new List<Type> { typeof(UniquePathCollection2) }
            };

            // Act & Assert - should not throw
            ValidateResourcePathUniqueness(resourcePathToCollections);
        }

        // Helper method to access the private ValidateResourcePathUniqueness method
        private static void ValidateResourcePathUniqueness(Dictionary<string, List<Type>> resourcePathToCollections)
        {
            Type resourceManagerType = typeof(ResourceManager);
            MethodInfo? validateMethod = resourceManagerType.GetMethod("ValidateResourcePathUniqueness",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (validateMethod == null)
            {
                throw new InvalidOperationException("ValidateResourcePathUniqueness method not found");
            }

            try
            {
                validateMethod.Invoke(null, new object[] { resourcePathToCollections });
            }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
            {
                // Re-throw the inner exception to preserve the original exception type
                throw ex.InnerException;
            }
        }

        // Test classes for validation testing (not actual resource collections)
        private class DuplicatePathCollection1 { }
        private class DuplicatePathCollection2 { }
        private class UniquePathCollection { }
        private class UniquePathCollection1 { }
        private class UniquePathCollection2 { }

        // Test resource collection that can be auto-registered (parameterless constructor)
        [ResourceCollection(typeof(ValidationFakeResourceProvider))]
        public static class AutoRegisteredResourceCollection
        {
            public static readonly Resource TestResource = new Resource("validation-auto.txt");
        }
    }
}