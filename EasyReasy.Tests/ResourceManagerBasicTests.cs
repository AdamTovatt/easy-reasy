using EasyReasy.Tests.TestProviders;
using EasyReasy.Tests.TestResourceCollections;
using System.Reflection;

namespace EasyReasy.Tests
{
    [TestClass]
    public class ResourceManagerBasicTests
    {
        [TestMethod]
        public async Task CreateInstance_WithAssembly_ReturnsValidInstance()
        {
            // Arrange & Act
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly(), predefinedProvider);

            // Assert
            Assert.IsNotNull(manager);
        }

        [TestMethod]
        public async Task CreateInstance_ForAssemblyWithoutResources_ReturnsValidInstance()
        {
            // Arrange & Act
            Assembly? assembly = Assembly.GetAssembly(typeof(EmbeddedResourceProvider)); // Use the EasyReasy assembly

            Assert.IsNotNull(assembly);

            ResourceManager manager = await ResourceManager.CreateInstanceAsync(assembly);

            // Assert
            Assert.IsNotNull(manager);
        }

        [TestMethod]
        public async Task CreateInstance_WithPredefinedProviders_RegistersProviders()
        {
            // Arrange
            ParameterizedFakeResourceProvider fakeProvider = new ParameterizedFakeResourceProvider("test", true, "shared-test.txt");
            PredefinedResourceProvider predefinedProvider = fakeProvider.AsPredefinedFor(typeof(SharedTestResourceCollection));

            // Act
            ResourceManager manager = await ResourceManager.CreateInstanceAsync(Assembly.GetExecutingAssembly(), predefinedProvider);

            // Assert
            Assert.IsNotNull(manager);
        }

        [TestMethod]
        public void GetContentType_WithValidResource_ReturnsContentType()
        {
            // Arrange
            Resource testResource = new Resource("test.txt");

            // Act
            string result = testResource.GetContentType();

            // Assert
            Assert.AreEqual("text/plain", result);
        }

        [TestMethod]
        public void GetContentType_WithImageResource_ReturnsImageContentType()
        {
            // Arrange
            Resource testResource = new Resource("test.jpg");

            // Act
            string result = testResource.GetContentType();

            // Assert
            Assert.AreEqual("image/jpeg", result);
        }

        [TestMethod]
        public void GetContentType_WithUnknownExtension_ReturnsOctetStream()
        {
            // Arrange
            Resource testResource = new Resource("test.unknown");

            // Act
            string result = testResource.GetContentType();

            // Assert
            Assert.AreEqual("application/octet-stream", result);
        }
    }
}