using System.Reflection;

namespace EasyReasy.Tests
{
    [TestClass]
    public class ResourceCollectionAttributeTests
    {
        [TestMethod]
        public void Constructor_WithProviderType_SetsProviderType()
        {
            // Arrange & Act
            ResourceCollectionAttribute attribute = new ResourceCollectionAttribute(typeof(MockResourceProvider));

            // Assert
            Assert.AreEqual(typeof(MockResourceProvider), attribute.ProviderType);
        }

        [TestMethod]
        public void Constructor_WithNullProviderType_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new ResourceCollectionAttribute(null!));
        }

        [TestMethod]
        public void ProviderType_ReturnsCorrectType()
        {
            // Arrange
            ResourceCollectionAttribute attribute = new ResourceCollectionAttribute(typeof(MockResourceProvider));

            // Act
            Type providerType = attribute.ProviderType;

            // Assert
            Assert.AreEqual(typeof(MockResourceProvider), providerType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrieved()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            ResourceCollectionAttribute? attribute = testClassType.GetCustomAttribute<ResourceCollectionAttribute>();

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(typeof(MockResourceProvider), attribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedWithInheritance()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            ResourceCollectionAttribute? attribute = testClassType.GetCustomAttribute<ResourceCollectionAttribute>(true);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(typeof(MockResourceProvider), attribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedWithoutInheritance()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            ResourceCollectionAttribute? attribute = testClassType.GetCustomAttribute<ResourceCollectionAttribute>(false);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.AreEqual(typeof(MockResourceProvider), attribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedAsCustomAttribute()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            Attribute? attribute = testClassType.GetCustomAttribute(typeof(ResourceCollectionAttribute));

            // Assert
            Assert.IsNotNull(attribute);
            Assert.IsInstanceOfType(attribute, typeof(ResourceCollectionAttribute));
            ResourceCollectionAttribute resourceAttribute = (ResourceCollectionAttribute)attribute;
            Assert.AreEqual(typeof(MockResourceProvider), resourceAttribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedAsCustomAttributeWithInheritance()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            Attribute? attribute = testClassType.GetCustomAttribute(typeof(ResourceCollectionAttribute), true);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.IsInstanceOfType(attribute, typeof(ResourceCollectionAttribute));
            ResourceCollectionAttribute resourceAttribute = (ResourceCollectionAttribute)attribute;
            Assert.AreEqual(typeof(MockResourceProvider), resourceAttribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedAsCustomAttributeWithoutInheritance()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            Attribute? attribute = testClassType.GetCustomAttribute(typeof(ResourceCollectionAttribute), false);

            // Assert
            Assert.IsNotNull(attribute);
            Assert.IsInstanceOfType(attribute, typeof(ResourceCollectionAttribute));
            ResourceCollectionAttribute resourceAttribute = (ResourceCollectionAttribute)attribute;
            Assert.AreEqual(typeof(MockResourceProvider), resourceAttribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedAsMultipleCustomAttributes()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            object[] attributes = testClassType.GetCustomAttributes(typeof(ResourceCollectionAttribute), true);

            // Assert
            Assert.IsNotNull(attributes);
            Assert.AreEqual(1, attributes.Length);
            Assert.IsInstanceOfType(attributes[0], typeof(ResourceCollectionAttribute));
            ResourceCollectionAttribute resourceAttribute = (ResourceCollectionAttribute)attributes[0];
            Assert.AreEqual(typeof(MockResourceProvider), resourceAttribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedAsMultipleCustomAttributesWithoutInheritance()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            object[] attributes = testClassType.GetCustomAttributes(typeof(ResourceCollectionAttribute), false);

            // Assert
            Assert.IsNotNull(attributes);
            Assert.AreEqual(1, attributes.Length);
            Assert.IsInstanceOfType(attributes[0], typeof(ResourceCollectionAttribute));
            ResourceCollectionAttribute resourceAttribute = (ResourceCollectionAttribute)attributes[0];
            Assert.AreEqual(typeof(MockResourceProvider), resourceAttribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedAsAllCustomAttributes()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            object[] attributes = testClassType.GetCustomAttributes(true);

            // Assert
            Assert.IsNotNull(attributes);
            Assert.IsTrue(attributes.Length > 0);
            ResourceCollectionAttribute? resourceAttribute = attributes.OfType<ResourceCollectionAttribute>().FirstOrDefault();
            Assert.IsNotNull(resourceAttribute);
            Assert.AreEqual(typeof(MockResourceProvider), resourceAttribute.ProviderType);
        }

        [TestMethod]
        public void Attribute_AppliedToClass_CanBeRetrievedAsAllCustomAttributesWithoutInheritance()
        {
            // Arrange
            Type testClassType = typeof(TestResourceCollection);

            // Act
            object[] attributes = testClassType.GetCustomAttributes(false);

            // Assert
            Assert.IsNotNull(attributes);
            Assert.IsTrue(attributes.Length > 0);
            ResourceCollectionAttribute? resourceAttribute = attributes.OfType<ResourceCollectionAttribute>().FirstOrDefault();
            Assert.IsNotNull(resourceAttribute);
            Assert.AreEqual(typeof(MockResourceProvider), resourceAttribute.ProviderType);
        }

        // Test resource collection for testing
        [ResourceCollection(typeof(MockResourceProvider))]
        public static class TestResourceCollection
        {
            public static readonly Resource TestResource = new Resource("test.txt");
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